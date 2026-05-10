import Ajv2020 from "ajv/dist/2020"

import envelopeSchema from "../../../../docs/schemas/protocol/v0.2/envelope.schema.json"
import errorEnvelopeSchema from "../../../../docs/schemas/protocol/v0.2/error-envelope.schema.json"
import eventAckSchema from "../../../../docs/schemas/protocol/v0.2/event-ack.schema.json"
import sessionResumeSchema from "../../../../docs/schemas/protocol/v0.2/session-resume.schema.json"
import workflowStartSchema from "../../../../docs/schemas/protocol/v0.2/workflow-start.schema.json"
import {
	CHROMA_PROTOCOL_VERSION,
	createEventAckEnvelope,
	createSessionResumeEnvelope,
	createSessionState,
	createWorkflowStartEnvelope,
	parseProtocolEnvelope,
	protocolEventNames,
	type ProtocolEnvelope,
} from "../protocol"

describe("backend bridge protocol schema compatibility", () => {
	const ajv = new Ajv2020({ strict: false, validateFormats: false })
	ajv.addSchema(envelopeSchema, "envelope.schema.json")

	it("validates extension-created workflow.start, event.ack, and session.resume envelopes", () => {
		const session = createSessionState()

		expect(ajv.validate(workflowStartSchema, createWorkflowStartEnvelope(session))).toBe(true)
		expect(ajv.validate(eventAckSchema, createEventAckEnvelope(session))).toBe(true)
		expect(ajv.validate(sessionResumeSchema, createSessionResumeEnvelope(session))).toBe(true)
	})

	it("validates and parses error envelopes", () => {
		const envelope: ProtocolEnvelope = {
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
			payload: {
				code: "future_sequence",
				message: "lastSeenSequence is ahead of the latest persisted workflow event.",
				retryable: false,
			},
		}

		expect(ajv.validate(errorEnvelopeSchema, envelope)).toBe(true)
		expect(parseProtocolEnvelope(envelope).ok).toBe(true)
	})

	it("parses connection.ready, workflow.started, and workflow.status", () => {
		const ids = {
			workspaceId: crypto.randomUUID(),
			workflowId: crypto.randomUUID(),
			sessionId: crypto.randomUUID(),
		}

		for (const envelope of [
			baseEnvelope(protocolEventNames.connectionReady, { workspaceId: null, workflowId: null, sessionId: null }),
			baseEnvelope(protocolEventNames.workflowStarted, { ...ids, sequence: 1 }),
			baseEnvelope(protocolEventNames.workflowStatus, { ...ids, sequence: 2 }),
		]) {
			expect(parseProtocolEnvelope(envelope).ok).toBe(true)
		}
	})
})

function baseEnvelope(
	name: string,
	overrides: Partial<Pick<ProtocolEnvelope, "workspaceId" | "workflowId" | "sessionId" | "sequence">> = {},
): ProtocolEnvelope {
	return {
		protocolVersion: CHROMA_PROTOCOL_VERSION,
		messageId: crypto.randomUUID(),
		workspaceId: overrides.workspaceId ?? crypto.randomUUID(),
		workflowId: overrides.workflowId ?? crypto.randomUUID(),
		sessionId: overrides.sessionId ?? crypto.randomUUID(),
		sequence: overrides.sequence ?? null,
		name,
		correlationId: null,
		idempotencyKey: null,
		timestamp: new Date().toISOString(),
		payload: {},
	}
}
