import { BackendBridgeSafeLogger, formatMetadata } from "../safeLogger"

describe("BackendBridgeSafeLogger", () => {
	it("formats safe metadata without payload bodies or multi-line output", () => {
		expect(
			formatMetadata({
				workflowId: "workflow-id",
				sessionId: "session-id",
				eventName: "workflow.started",
				sequence: 1,
				errorCode: "future_sequence",
				summary: "safe\nsummary",
			}),
		).toBe(
			" workflowId=workflow-id sessionId=session-id eventName=workflow.started sequence=1 errorCode=future_sequence summary=safe summary",
		)
	})

	it("writes safe output channel lines", () => {
		const lines: string[] = []
		const logger = new BackendBridgeSafeLogger({
			appendLine: (line: string) => lines.push(line),
			show: () => {},
		} as any)

		logger.info("event received", { eventName: "workflow.status", sequence: 2 })

		expect(lines).toEqual(["[Chroma Backend] event received eventName=workflow.status sequence=2"])
	})
})
