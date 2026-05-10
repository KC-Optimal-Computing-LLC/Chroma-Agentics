import * as assert from "assert"
import * as vscode from "vscode"

import { setDefaultSuiteTimeout } from "./test-utils"
import { waitFor } from "./utils"

const backendBridgeCommandIds = [
	"chromaAgentics.backend.setToken",
	"chromaAgentics.backend.clearToken",
	"chromaAgentics.backend.health",
	"chromaAgentics.backend.connect",
	"chromaAgentics.backend.startSmokeWorkflow",
	"chromaAgentics.backend.disconnect",
] as const

suite("Chroma Backend Bridge", function () {
	setDefaultSuiteTimeout(this)

	const backendUrl = process.env.CHROMA_BACKEND_URL ?? "http://localhost:5127"
	const backendToken = process.env.CHROMA_DEV_AUTH_TOKEN ?? "change-me-local-dev-token"

	async function getHandle() {
		await waitFor(() => Boolean(globalThis.__chromaBackendBridgeTestHandle))
		return globalThis.__chromaBackendBridgeTestHandle!
	}

	async function configureBridge(enabled: boolean) {
		const configuration = vscode.workspace.getConfiguration()
		await configuration.update("chromaAgentics.backend.enabled", enabled, vscode.ConfigurationTarget.Workspace)
		await configuration.update("chromaAgentics.backend.url", backendUrl, vscode.ConfigurationTarget.Workspace)
		await configuration.update(
			"chromaAgentics.backend.connectionTimeoutMs",
			5000,
			vscode.ConfigurationTarget.Workspace,
		)
		await configuration.update(
			"chromaAgentics.backend.reconnect.enabled",
			true,
			vscode.ConfigurationTarget.Workspace,
		)
		await configuration.update(
			"chromaAgentics.backend.reconnect.maxAttempts",
			5,
			vscode.ConfigurationTarget.Workspace,
		)
		await configuration.update(
			"chromaAgentics.backend.reconnect.initialDelayMs",
			1000,
			vscode.ConfigurationTarget.Workspace,
		)
	}

	setup(async () => {
		await configureBridge(false)
		await vscode.commands.executeCommand("chromaAgentics.backend.clearToken")
		const handle = await getHandle()
		await handle.clearToken()
	})

	teardown(async () => {
		await vscode.commands.executeCommand("chromaAgentics.backend.disconnect")
		await vscode.commands.executeCommand("chromaAgentics.backend.clearToken")
		const handle = await getHandle()
		await handle.clearToken()
		await configureBridge(false)
	})

	test("runs the backend bridge smoke flow through Extension Development Host commands", async () => {
		const commands = new Set(await vscode.commands.getCommands(true))
		for (const commandId of backendBridgeCommandIds) {
			assert.ok(commands.has(commandId), `Expected command ${commandId} to be registered`)
		}

		const handle = await getHandle()
		await configureBridge(true)

		await vscode.commands.executeCommand("chromaAgentics.backend.setToken", backendToken)
		await waitFor(() =>
			handle.logger.getEntriesForTests().some((line) => line.includes("token stored in SecretStorage")),
		)

		await vscode.commands.executeCommand("chromaAgentics.backend.health")
		await waitFor(() => handle.logger.getEntriesForTests().some((line) => line.includes("health checked")))
		assert.strictEqual(handle.statusBar.getStateForTests().status, "connected")

		await vscode.commands.executeCommand("chromaAgentics.backend.connect")
		await waitFor(() =>
			handle.logger.getEntriesForTests().some((line) => line.includes("connection.ready received")),
		)
		assert.strictEqual(handle.statusBar.getStateForTests().status, "connected")

		await vscode.commands.executeCommand("chromaAgentics.backend.startSmokeWorkflow")
		await waitFor(() => {
			const session = handle.client.getSessionForTests()
			return Boolean(session && session.lastSeenSequence >= 2)
		})

		const session = handle.client.getSessionForTests()
		assert.ok(session, "Expected smoke workflow session to exist")
		assert.ok(session.processedMessageIds.size >= 2, "Expected durable events to be processed")
		assert.ok(
			handle.logger
				.getEntriesForTests()
				.some((line) => line.includes("workflow.start sent") && line.includes("eventName=workflow.start")),
			"Expected workflow.start log entry",
		)
		assert.ok(
			handle.logger.getEntriesForTests().some((line) => line.includes("eventName=workflow.started")),
			"Expected workflow.started log entry",
		)
		assert.ok(
			handle.logger
				.getEntriesForTests()
				.some((line) => line.includes("event.ack sent") && line.includes("sequence=2")),
			"Expected event.ack log entry for the durable workflow status",
		)

		const processedCountBeforeReplay = session.processedMessageIds.size
		const logCountBeforeReconnect = handle.logger.getEntriesForTests().length
		session.lastSeenSequence = 1

		await vscode.commands.executeCommand("chromaAgentics.backend.disconnect")
		await waitFor(() => handle.statusBar.getStateForTests().status === "disconnected")

		await vscode.commands.executeCommand("chromaAgentics.backend.connect")
		await waitFor(() =>
			handle.logger
				.getEntriesForTests()
				.some(
					(line, index) =>
						index >= logCountBeforeReconnect &&
						line.includes("session.resume sent") &&
						line.includes("sequence=1"),
				),
		)
		await waitFor(() =>
			handle.logger
				.getEntriesForTests()
				.some(
					(line, index) => index >= logCountBeforeReconnect && line.includes("duplicate replay suppressed"),
				),
		)

		assert.strictEqual(
			handle.client.getSessionForTests()?.processedMessageIds.size,
			processedCountBeforeReplay,
			"Duplicate replay should not create a second processed event record",
		)
	})
})
