import WebSocket from "ws"

import { buildHealthUrl, buildWebSocketUrl, type BackendBridgeConfig } from "./config"
import {
	createEventAckEnvelope,
	createSessionResumeEnvelope,
	createSessionState,
	createWorkflowStartEnvelope,
	getProcessedEventKey,
	isDurableProtocolEvent,
	parseProtocolJson,
	protocolEventNames,
	type BackendBridgeConnectionState,
	type BackendBridgeSessionState,
	type ProtocolEnvelope,
} from "./protocol"
import type { BackendBridgeTokenStore } from "./secrets"
import type { BackendBridgeSafeLogger } from "./safeLogger"

const WebSocketOpenState = 1

export type BackendBridgeWebSocket = {
	readyState: number
	send(data: string): void
	close(): void
	on(event: "open", listener: () => void): BackendBridgeWebSocket
	on(event: "message", listener: (data: unknown) => void): BackendBridgeWebSocket
	on(event: "close", listener: () => void): BackendBridgeWebSocket
	on(event: "error", listener: (error: Error) => void): BackendBridgeWebSocket
}

export type BackendBridgeWebSocketFactory = (
	url: string,
	options: { headers: Record<string, string> },
) => BackendBridgeWebSocket

export type BackendBridgeStatusSink = {
	setStatus(status: BackendBridgeConnectionState, detail?: string): void
}

export type BackendBridgeClientOptions = {
	readConfig: () => BackendBridgeConfig
	tokenStore: BackendBridgeTokenStore
	logger: BackendBridgeSafeLogger
	status: BackendBridgeStatusSink
	webSocketFactory?: BackendBridgeWebSocketFactory
	fetchImpl?: typeof fetch
	setTimeoutImpl?: typeof setTimeout
	clearTimeoutImpl?: typeof clearTimeout
}

export type HealthCheckResult = {
	ok: boolean
	live?: number
	ready?: number
	dependencies?: number
	errorCode?: string
}

export class BackendBridgeClient {
	private socket: BackendBridgeWebSocket | undefined
	private session: BackendBridgeSessionState | undefined
	private manualDisconnect = false
	private suppressCloseReconnect = false
	private reconnectAttempts = 0

	public constructor(private readonly options: BackendBridgeClientOptions) {}

	public getSessionForTests(): BackendBridgeSessionState | undefined {
		return this.session
	}

	public async healthCheck(): Promise<HealthCheckResult> {
		const config = this.options.readConfig()

		try {
			const [live, ready, dependencies] = await Promise.all([
				this.fetchHealthStatus(config, "live"),
				this.fetchHealthStatus(config, "ready"),
				this.fetchHealthStatus(config, "dependencies"),
			])
			const ok = live >= 200 && live < 300 && ready >= 200 && ready < 300
			this.setStatus(ok ? "connected" : "unhealthy", `ready=${ready}`)
			this.options.logger.info("health checked", {
				backendStatus: ok ? "healthy" : "unhealthy",
				summary: `live=${live} ready=${ready} dependencies=${dependencies}`,
			})
			return { ok, live, ready, dependencies }
		} catch {
			this.setStatus("unhealthy", "backend offline")
			this.options.logger.error("health check failed", { errorCode: "backend_offline" })
			return { ok: false, errorCode: "backend_offline" }
		}
	}

	public async connect(userInitiated = false): Promise<boolean> {
		const config = this.options.readConfig()
		if (!config.enabled && !userInitiated) {
			this.setStatus("disabled")
			return false
		}

		const token = await this.options.tokenStore.getToken()
		if (!token) {
			this.setStatus("auth failed", "missing token")
			this.options.logger.error("connection rejected", { errorCode: "missing_token" })
			return false
		}

		this.manualDisconnect = false
		this.setStatus(this.reconnectAttempts > 0 ? "reconnecting" : "connecting")

		let url: string
		try {
			url = buildWebSocketUrl(config.url)
		} catch {
			this.setStatus("error", "invalid backend URL")
			this.options.logger.error("connection rejected", { errorCode: "invalid_backend_url" })
			return false
		}

		if (this.socket) {
			this.suppressCloseReconnect = true
			this.socket.close()
		}

		return new Promise<boolean>((resolve) => {
			let settled = false
			const timeout = this.setTimeout(() => {
				if (settled) {
					return
				}
				settled = true
				this.setStatus("error", "connection timed out")
				this.options.logger.error("connection failed", { errorCode: "connection_timeout" })
				this.socket?.close()
				resolve(false)
			}, config.connectionTimeoutMs)

			const socket =
				this.options.webSocketFactory?.(url, { headers: authHeaders(token) }) ?? createWsClient(url, token)
			this.socket = socket

			socket.on("open", () => {
				if (!settled) {
					settled = true
					this.clearTimeout(timeout)
					this.reconnectAttempts = 0
					this.setStatus("connected")
					this.options.logger.info("event stream connected")
					this.resumeIfPossible()
					resolve(true)
				}
			})

			socket.on("message", (data) => this.handleMessage(data))

			socket.on("close", () => {
				if (!settled) {
					settled = true
					this.clearTimeout(timeout)
					this.setStatus("auth failed", "connection closed before open")
					this.options.logger.error("connection failed", { errorCode: "connection_closed" })
					resolve(false)
					return
				}

				this.handleClose()
			})

			socket.on("error", () => {
				if (!settled) {
					settled = true
					this.clearTimeout(timeout)
					this.setStatus("auth failed", "connection error")
					this.options.logger.error("connection failed", { errorCode: "connection_error" })
					resolve(false)
				}
			})
		})
	}

	public async startSmokeWorkflow(): Promise<boolean> {
		this.session = createSessionState()

		if (!this.isOpen()) {
			const connected = await this.connect(true)
			if (!connected) {
				return false
			}
		}

		const envelope = createWorkflowStartEnvelope(this.session)
		this.sendEnvelope(envelope)
		this.setStatus("workflow started")
		this.options.logger.info("workflow.start sent", {
			workflowId: envelope.workflowId,
			sessionId: envelope.sessionId,
			eventName: envelope.name,
		})
		return true
	}

	public disconnect(): void {
		this.manualDisconnect = true
		this.socket?.close()
		this.socket = undefined
		this.setStatus("disconnected")
		this.options.logger.info("event stream disconnected")
	}

	private async fetchHealthStatus(
		config: BackendBridgeConfig,
		path: "live" | "ready" | "dependencies",
	): Promise<number> {
		const controller = new AbortController()
		const timeout = this.setTimeout(() => controller.abort(), config.connectionTimeoutMs)
		try {
			const response = await (this.options.fetchImpl ?? fetch)(buildHealthUrl(config.url, path), {
				method: "GET",
				signal: controller.signal,
			})
			return response.status
		} finally {
			this.clearTimeout(timeout)
		}
	}

	private handleMessage(data: unknown): void {
		const parsed = parseProtocolJson(toText(data))
		if (!parsed.ok) {
			this.setStatus("error", parsed.errorCode)
			this.options.logger.error("backend event rejected", {
				errorCode: parsed.errorCode,
				summary: parsed.summary,
			})
			return
		}

		const envelope = parsed.envelope
		if (envelope.name === protocolEventNames.error) {
			const code = typeof envelope.payload.code === "string" ? envelope.payload.code : "unknown"
			this.session = this.session ? { ...this.session, lastErrorCode: code } : this.session
			this.setStatus(code === "unauthorized" ? "auth failed" : "error", code)
			this.options.logger.error("backend error", {
				workflowId: envelope.workflowId,
				sessionId: envelope.sessionId,
				eventName: envelope.name,
				errorCode: code,
				summary: typeof envelope.payload.message === "string" ? envelope.payload.message : undefined,
			})
			return
		}

		if (envelope.name === protocolEventNames.connectionReady) {
			this.setStatus("connected")
			this.options.logger.info("connection.ready received", { eventName: envelope.name })
			return
		}

		if (this.handleNonDurableStatus(envelope)) {
			return
		}

		if (!isDurableProtocolEvent(envelope)) {
			this.options.logger.info("non-durable event received", {
				eventName: envelope.name,
				workflowId: envelope.workflowId,
				sessionId: envelope.sessionId,
			})
			return
		}

		if (!this.session && envelope.workspaceId && envelope.workflowId && envelope.sessionId) {
			this.session = {
				workspaceId: envelope.workspaceId,
				workflowId: envelope.workflowId,
				sessionId: envelope.sessionId,
				lastSeenSequence: 0,
				processedMessageIds: new Set<string>(),
				connectionState: "connected",
			}
		}

		if (!this.session) {
			this.setStatus("error", "missing session")
			this.options.logger.error("durable event rejected", {
				errorCode: "missing_session",
				eventName: envelope.name,
			})
			return
		}

		const key = getProcessedEventKey(envelope)
		const duplicate = key ? this.session.processedMessageIds.has(key) : false
		if (key) {
			this.session.processedMessageIds.add(key)
		}

		this.session.lastSeenSequence = Math.max(this.session.lastSeenSequence, envelope.sequence ?? 0)
		this.setStatus(envelope.name === protocolEventNames.workflowStarted ? "workflow started" : "event received")
		this.options.logger.info(duplicate ? "duplicate replay suppressed" : "event received", {
			workflowId: envelope.workflowId,
			sessionId: envelope.sessionId,
			eventName: envelope.name,
			sequence: envelope.sequence,
		})
		this.sendAck()
	}

	private handleNonDurableStatus(envelope: ProtocolEnvelope): boolean {
		if (envelope.name !== protocolEventNames.workflowStatus || envelope.sequence !== null) {
			return false
		}

		const status = typeof envelope.payload.status === "string" ? envelope.payload.status : undefined
		if (status === "ack.updated" || status === "ack.noop") {
			this.setStatus("ACK sent")
			this.options.logger.info("ACK status received", {
				workflowId: envelope.workflowId,
				sessionId: envelope.sessionId,
				eventName: envelope.name,
				summary: status,
			})
			return true
		}

		if (status === "resume.current") {
			this.setStatus("resume complete")
			this.options.logger.info("resume complete", {
				workflowId: envelope.workflowId,
				sessionId: envelope.sessionId,
				eventName: envelope.name,
			})
			return true
		}

		return false
	}

	private handleClose(): void {
		this.socket = undefined
		if (this.manualDisconnect || this.suppressCloseReconnect) {
			this.suppressCloseReconnect = false
			return
		}

		const config = this.options.readConfig()
		if (!config.enabled || !config.reconnect.enabled || this.reconnectAttempts >= config.reconnect.maxAttempts) {
			this.setStatus("disconnected")
			return
		}

		this.reconnectAttempts += 1
		this.setStatus("reconnecting", `attempt ${this.reconnectAttempts}`)
		const delayMs = config.reconnect.initialDelayMs * 2 ** Math.max(0, this.reconnectAttempts - 1)
		this.setTimeout(() => void this.connect(false), delayMs)
	}

	private resumeIfPossible(): void {
		if (!this.session) {
			return
		}

		this.sendEnvelope(createSessionResumeEnvelope(this.session))
		this.options.logger.info("session.resume sent", {
			workflowId: this.session.workflowId,
			sessionId: this.session.sessionId,
			sequence: this.session.lastSeenSequence,
		})
	}

	private sendAck(): void {
		if (!this.session) {
			return
		}

		this.sendEnvelope(createEventAckEnvelope(this.session))
		this.options.logger.info("event.ack sent", {
			workflowId: this.session.workflowId,
			sessionId: this.session.sessionId,
			sequence: this.session.lastSeenSequence,
		})
	}

	private sendEnvelope(envelope: ProtocolEnvelope): void {
		if (!this.isOpen()) {
			this.setStatus("disconnected", "socket not open")
			return
		}

		this.socket?.send(JSON.stringify(envelope))
	}

	private isOpen(): boolean {
		return this.socket?.readyState === WebSocketOpenState
	}

	private setStatus(status: BackendBridgeConnectionState, detail?: string): void {
		if (this.session) {
			this.session.connectionState = status
		}
		this.options.status.setStatus(status, detail)
	}

	private setTimeout(callback: () => void, ms: number): ReturnType<typeof setTimeout> {
		return (this.options.setTimeoutImpl ?? setTimeout)(callback, ms)
	}

	private clearTimeout(timeout: ReturnType<typeof setTimeout>): void {
		;(this.options.clearTimeoutImpl ?? clearTimeout)(timeout)
	}
}

export function authHeaders(token: string): Record<string, string> {
	return { "X-Chroma-Dev-Token": token }
}

function createWsClient(url: string, token: string): BackendBridgeWebSocket {
	return new WebSocket(url, { headers: authHeaders(token) }) as unknown as BackendBridgeWebSocket
}

function toText(data: unknown): string {
	if (typeof data === "string") {
		return data
	}

	if (Buffer.isBuffer(data)) {
		return data.toString("utf8")
	}

	if (data instanceof ArrayBuffer) {
		return Buffer.from(data).toString("utf8")
	}

	return String(data)
}
