import { randomUUID } from "node:crypto"

export const CHROMA_PROTOCOL_VERSION = "0.2"

export const protocolEventNames = {
	workflowStart: "workflow.start",
	sessionResume: "session.resume",
	eventAck: "event.ack",
	connectionReady: "connection.ready",
	workflowStarted: "workflow.started",
	workflowStatus: "workflow.status",
	error: "error",
} as const

export type ProtocolEnvelope<TPayload extends Record<string, unknown> = Record<string, unknown>> = {
	protocolVersion: string
	messageId: string
	workspaceId: string | null
	workflowId: string | null
	sessionId: string | null
	sequence: number | null
	name: string
	correlationId: string | null
	idempotencyKey: string | null
	timestamp: string
	payload: TPayload
}

export type BackendBridgeSessionState = {
	workspaceId: string
	workflowId: string
	sessionId: string
	lastSeenSequence: number
	processedMessageIds: Set<string>
	connectionState: BackendBridgeConnectionState
	lastErrorCode?: string
}

export type BackendBridgeConnectionState =
	| "disabled"
	| "disconnected"
	| "connecting"
	| "connected"
	| "unhealthy"
	| "auth failed"
	| "workflow started"
	| "event received"
	| "ACK sent"
	| "reconnecting"
	| "resume complete"
	| "error"

export type WorkflowStartPayload = {
	title: string
	mode: string
	source: string
	clientName: string
}

export type AckPayload = {
	lastSeenSequence: number
}

export type ResumePayload = {
	lastSeenSequence: number
}

export type ParsedProtocolEnvelope =
	| { ok: true; envelope: ProtocolEnvelope }
	| { ok: false; errorCode: string; summary: string }

export function createSessionState(): BackendBridgeSessionState {
	return {
		workspaceId: randomUUID(),
		workflowId: randomUUID(),
		sessionId: randomUUID(),
		lastSeenSequence: 0,
		processedMessageIds: new Set<string>(),
		connectionState: "disconnected",
	}
}

export function createWorkflowStartEnvelope(
	session: BackendBridgeSessionState,
	payload: WorkflowStartPayload = {
		title: "Extension bridge smoke workflow",
		mode: "orchestrator",
		source: "vscode-extension-smoke",
		clientName: "vscode-extension",
	},
): ProtocolEnvelope<WorkflowStartPayload> {
	return createEnvelope(session, protocolEventNames.workflowStart, payload, `smoke-${session.workflowId}`)
}

export function createEventAckEnvelope(
	session: BackendBridgeSessionState,
	lastSeenSequence = session.lastSeenSequence,
): ProtocolEnvelope<AckPayload> {
	return createEnvelope(session, protocolEventNames.eventAck, { lastSeenSequence }, null)
}

export function createSessionResumeEnvelope(session: BackendBridgeSessionState): ProtocolEnvelope<ResumePayload> {
	return createEnvelope(
		session,
		protocolEventNames.sessionResume,
		{ lastSeenSequence: session.lastSeenSequence },
		null,
	)
}

export function parseProtocolEnvelope(value: unknown): ParsedProtocolEnvelope {
	if (!isRecord(value)) {
		return { ok: false, errorCode: "invalid_json", summary: "Protocol message must be an object." }
	}

	if (value.protocolVersion !== CHROMA_PROTOCOL_VERSION) {
		return { ok: false, errorCode: "bad_protocol_version", summary: "Unsupported protocol version." }
	}

	if (typeof value.name !== "string" || value.name.length === 0) {
		return { ok: false, errorCode: "unknown_message_name", summary: "Protocol message name is missing." }
	}

	if (typeof value.messageId !== "string" || value.messageId.length === 0) {
		return { ok: false, errorCode: "missing_required_field", summary: "Protocol messageId is missing." }
	}

	if (!(value.sequence === null || Number.isInteger(value.sequence))) {
		return { ok: false, errorCode: "invalid_id", summary: "Protocol sequence must be an integer or null." }
	}

	if (!isRecord(value.payload)) {
		return { ok: false, errorCode: "missing_required_field", summary: "Protocol payload must be an object." }
	}

	return { ok: true, envelope: value as ProtocolEnvelope }
}

export function parseProtocolJson(json: string): ParsedProtocolEnvelope {
	try {
		return parseProtocolEnvelope(JSON.parse(json))
	} catch {
		return { ok: false, errorCode: "invalid_json", summary: "Backend message was not valid JSON." }
	}
}

export function getProcessedEventKey(envelope: ProtocolEnvelope): string | undefined {
	if (!envelope.workflowId || envelope.sequence === null) {
		return undefined
	}

	return `${envelope.workflowId}:${envelope.sequence}:${envelope.messageId}`
}

export function isDurableProtocolEvent(envelope: ProtocolEnvelope): boolean {
	return envelope.sequence !== null && envelope.workflowId !== null
}

function createEnvelope<TPayload extends Record<string, unknown>>(
	session: BackendBridgeSessionState,
	name: string,
	payload: TPayload,
	idempotencyKey: string | null,
): ProtocolEnvelope<TPayload> {
	return {
		protocolVersion: CHROMA_PROTOCOL_VERSION,
		messageId: randomUUID(),
		workspaceId: session.workspaceId,
		workflowId: session.workflowId,
		sessionId: session.sessionId,
		sequence: null,
		name,
		correlationId: randomUUID(),
		idempotencyKey,
		timestamp: new Date().toISOString(),
		payload,
	}
}

function isRecord(value: unknown): value is Record<string, unknown> {
	return typeof value === "object" && value !== null && !Array.isArray(value)
}
