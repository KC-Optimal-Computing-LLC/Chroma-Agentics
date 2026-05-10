import {
	CHROMA_PROTOCOL_VERSION,
	createEventAckEnvelope,
	createSessionResumeEnvelope,
	createSessionState,
	createWorkflowStartEnvelope,
	getProcessedEventKey,
	parseProtocolEnvelope,
	protocolEventNames,
} from "../protocol"

describe("backend bridge protocol helpers", () => {
	it("creates protocol 0.2 workflow.start envelopes with client-generated IDs", () => {
		const session = createSessionState()
		const envelope = createWorkflowStartEnvelope(session)

		expect(envelope.protocolVersion).toBe(CHROMA_PROTOCOL_VERSION)
		expect(envelope.name).toBe(protocolEventNames.workflowStart)
		expect(envelope.workspaceId).toBe(session.workspaceId)
		expect(envelope.workflowId).toBe(session.workflowId)
		expect(envelope.sessionId).toBe(session.sessionId)
		expect(envelope.sequence).toBeNull()
		expect(envelope.idempotencyKey).toBe(`smoke-${session.workflowId}`)
	})

	it("creates ACK and resume payloads from lastSeenSequence", () => {
		const session = createSessionState()
		session.lastSeenSequence = 2

		expect(createEventAckEnvelope(session).payload).toEqual({ lastSeenSequence: 2 })
		expect(createSessionResumeEnvelope(session).payload).toEqual({ lastSeenSequence: 2 })
	})

	it("parses expected inbound backend messages", () => {
		const base = {
			protocolVersion: CHROMA_PROTOCOL_VERSION,
			messageId: crypto.randomUUID(),
			workspaceId: crypto.randomUUID(),
			workflowId: crypto.randomUUID(),
			sessionId: crypto.randomUUID(),
			sequence: null,
			correlationId: null,
			idempotencyKey: null,
			timestamp: new Date().toISOString(),
			payload: {},
		}

		for (const name of [
			protocolEventNames.connectionReady,
			protocolEventNames.workflowStarted,
			protocolEventNames.workflowStatus,
			protocolEventNames.error,
		]) {
			expect(parseProtocolEnvelope({ ...base, name }).ok).toBe(true)
		}
	})

	it("rejects protocol version drift", () => {
		const result = parseProtocolEnvelope({
			protocolVersion: "1.0",
			messageId: crypto.randomUUID(),
			workspaceId: null,
			workflowId: null,
			sessionId: null,
			sequence: null,
			name: protocolEventNames.connectionReady,
			timestamp: new Date().toISOString(),
			payload: {},
		})

		expect(result).toMatchObject({ ok: false, errorCode: "bad_protocol_version" })
	})

	it("uses workflowId, sequence, and messageId for replay duplicate keys", () => {
		const workflowId = crypto.randomUUID()
		const messageId = crypto.randomUUID()

		expect(
			getProcessedEventKey({
				protocolVersion: CHROMA_PROTOCOL_VERSION,
				messageId,
				workspaceId: crypto.randomUUID(),
				workflowId,
				sessionId: crypto.randomUUID(),
				sequence: 4,
				name: protocolEventNames.workflowStatus,
				correlationId: null,
				idempotencyKey: null,
				timestamp: new Date().toISOString(),
				payload: {},
			}),
		).toBe(`${workflowId}:4:${messageId}`)
	})
})
