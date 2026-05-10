import { BackendBridgeClient, authHeaders, type BackendBridgeWebSocket } from "../BackendBridgeClient"
import { defaultBackendBridgeConfig, type BackendBridgeConfig } from "../config"
import { CHROMA_PROTOCOL_VERSION, protocolEventNames, type ProtocolEnvelope } from "../protocol"

class FakeSocket implements BackendBridgeWebSocket {
	public readyState = 0
	public sent: string[] = []
	private readonly handlers = new Map<string, Array<(...args: any[]) => void>>()

	public on(
		event: "open" | "message" | "close" | "error",
		listener: (...args: any[]) => void,
	): BackendBridgeWebSocket {
		const listeners = this.handlers.get(event) ?? []
		listeners.push(listener)
		this.handlers.set(event, listeners)
		return this
	}

	public send(data: string): void {
		this.sent.push(data)
	}

	public close(): void {
		this.readyState = 3
		this.emit("close")
	}

	public open(): void {
		this.readyState = 1
		this.emit("open")
	}

	public message(envelope: ProtocolEnvelope): void {
		this.emit("message", JSON.stringify(envelope))
	}

	private emit(event: string, ...args: any[]): void {
		for (const listener of this.handlers.get(event) ?? []) {
			listener(...args)
		}
	}
}

describe("BackendBridgeClient", () => {
	it("does not auto-connect when disabled", async () => {
		let socketCreated = false
		const client = createClient({
			config: { ...defaultBackendBridgeConfig, enabled: false },
			token: "token",
			webSocketFactory: () => {
				socketCreated = true
				return new FakeSocket()
			},
		})

		await expect(client.connect(false)).resolves.toBe(false)
		expect(socketCreated).toBe(false)
	})

	it("reports missing token without creating a WebSocket", async () => {
		let socketCreated = false
		const harness = createHarness()

		const client = createClient({
			token: null,
			status: harness.status,
			logger: harness.logger,
			webSocketFactory: () => {
				socketCreated = true
				return new FakeSocket()
			},
		})

		await expect(client.connect(true)).resolves.toBe(false)
		expect(socketCreated).toBe(false)
		expect(harness.status.statuses.at(-1)?.status).toBe("auth failed")
		expect(harness.logger.lines.join("\n")).not.toContain("secret-token")
	})

	it("uses X-Chroma-Dev-Token auth headers", async () => {
		const socket = new FakeSocket()
		let headers: Record<string, string> = {}
		const client = createClient({
			token: "secret-token",
			webSocketFactory: (_url: string, options: { headers: Record<string, string> }) => {
				headers = options.headers
				return socket
			},
		})

		const connection = client.connect(true)
		await Promise.resolve()
		socket.open()

		await expect(connection).resolves.toBe(true)
		expect(headers).toEqual(authHeaders("secret-token"))
	})

	it("sends workflow.start and ACKs durable workflow events after processing", async () => {
		const socket = new FakeSocket()
		const client = createClient({ webSocketFactory: () => socket })
		const connection = client.connect(true)
		await Promise.resolve()
		socket.open()
		await connection

		await client.startSmokeWorkflow()
		const start = JSON.parse(socket.sent.at(-1)!) as ProtocolEnvelope
		expect(start.name).toBe(protocolEventNames.workflowStart)
		expect(start.sequence).toBeNull()

		socket.message(durableEnvelope(start, protocolEventNames.workflowStarted, 1))
		const ack = JSON.parse(socket.sent.at(-1)!) as ProtocolEnvelope
		expect(ack.name).toBe(protocolEventNames.eventAck)
		expect(ack.payload).toEqual({ lastSeenSequence: 1 })
	})

	it("suppresses duplicate replay display while still ACKing ignored duplicates", async () => {
		const socket = new FakeSocket()
		const harness = createHarness()
		const client = createClient({
			logger: harness.logger,
			status: harness.status,
			webSocketFactory: () => socket,
		})
		const connection = client.connect(true)
		await Promise.resolve()
		socket.open()
		await connection
		await client.startSmokeWorkflow()
		const start = JSON.parse(socket.sent.at(-1)!) as ProtocolEnvelope
		const event = durableEnvelope(start, protocolEventNames.workflowStatus, 2)

		socket.message(event)
		socket.message(event)

		expect((harness.logger.lines as string[]).some((line) => line.includes("duplicate replay suppressed"))).toBe(
			true,
		)
		expect(
			socket.sent.filter((item) => (JSON.parse(item) as ProtocolEnvelope).name === protocolEventNames.eventAck),
		).toHaveLength(2)
	})

	it("sends session.resume on reconnect with memory lastSeenSequence", async () => {
		const first = new FakeSocket()
		const second = new FakeSocket()
		let socketIndex = 0
		const client = createClient({
			webSocketFactory: () => (socketIndex++ === 0 ? first : second),
		})
		let connection = client.connect(true)
		await Promise.resolve()
		first.open()
		await connection
		await client.startSmokeWorkflow()
		const start = JSON.parse(first.sent.at(-1)!) as ProtocolEnvelope
		first.message(durableEnvelope(start, protocolEventNames.workflowStarted, 1))
		client.disconnect()

		connection = client.connect(true)
		await Promise.resolve()
		second.open()
		await connection

		const resume = JSON.parse(second.sent.at(-1)!) as ProtocolEnvelope
		expect(resume.name).toBe(protocolEventNames.sessionResume)
		expect(resume.payload).toEqual({ lastSeenSequence: 1 })
	})

	it("handles backend error envelopes safely", async () => {
		const socket = new FakeSocket()
		const harness = createHarness()
		const client = createClient({
			logger: harness.logger,
			status: harness.status,
			webSocketFactory: () => socket,
		})
		const connection = client.connect(true)
		await Promise.resolve()
		socket.open()
		await connection

		socket.message({
			protocolVersion: CHROMA_PROTOCOL_VERSION,
			messageId: crypto.randomUUID(),
			workspaceId: null,
			workflowId: null,
			sessionId: null,
			sequence: null,
			name: protocolEventNames.error,
			correlationId: null,
			idempotencyKey: null,
			timestamp: new Date().toISOString(),
			payload: { code: "future_sequence", message: "lastSeenSequence is ahead.", retryable: false },
		})

		expect(harness.status.statuses.at(-1)).toMatchObject({ status: "error", detail: "future_sequence" })
		expect(harness.logger.lines.join("\n")).toContain("errorCode=future_sequence")
	})
})

function createClient(
	options: {
		config?: BackendBridgeConfig
		token?: string | null
		webSocketFactory?: any
		logger?: ReturnType<typeof createHarness>["logger"]
		status?: ReturnType<typeof createHarness>["status"]
	} = {},
): BackendBridgeClient {
	const harness = createHarness()
	return new BackendBridgeClient({
		readConfig: () => options.config ?? { ...defaultBackendBridgeConfig, enabled: true },
		tokenStore: {
			getToken: async () => ("token" in options ? (options.token ?? undefined) : "token"),
			setToken: async () => {},
			clearToken: async () => {},
		} as any,
		logger: options.logger ?? harness.logger,
		status: options.status ?? harness.status,
		webSocketFactory: options.webSocketFactory,
	})
}

function createHarness() {
	return {
		logger: {
			lines: [] as string[],
			info(message: string, metadata: Record<string, unknown> = {}) {
				this.lines.push(
					`${message} ${Object.entries(metadata)
						.map(([key, value]) => `${key}=${value}`)
						.join(" ")}`,
				)
			},
			error(message: string, metadata: Record<string, unknown> = {}) {
				this.lines.push(
					`${message} ${Object.entries(metadata)
						.map(([key, value]) => `${key}=${value}`)
						.join(" ")}`,
				)
			},
			show() {},
		} as any,
		status: {
			statuses: [] as Array<{ status: string; detail?: string }>,
			setStatus(status: string, detail?: string) {
				this.statuses.push({ status, detail })
			},
		} as any,
	}
}

function durableEnvelope(start: ProtocolEnvelope, name: string, sequence: number): ProtocolEnvelope {
	return {
		protocolVersion: CHROMA_PROTOCOL_VERSION,
		messageId: crypto.randomUUID(),
		workspaceId: start.workspaceId,
		workflowId: start.workflowId,
		sessionId: start.sessionId,
		sequence,
		name,
		correlationId: start.correlationId,
		idempotencyKey: start.idempotencyKey,
		timestamp: new Date().toISOString(),
		payload: {},
	}
}
