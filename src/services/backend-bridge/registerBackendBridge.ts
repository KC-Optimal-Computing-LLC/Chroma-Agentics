import * as vscode from "vscode"

import { BackendBridgeClient } from "./BackendBridgeClient"
import { readBackendBridgeConfig } from "./config"
import { BackendBridgeSafeLogger } from "./safeLogger"
import { BackendBridgeTokenStore } from "./secrets"
import { BackendBridgeStatusBar } from "./statusBar"

export const backendBridgeCommandIds = {
	setToken: "chromaAgentics.backend.setToken",
	clearToken: "chromaAgentics.backend.clearToken",
	health: "chromaAgentics.backend.health",
	connect: "chromaAgentics.backend.connect",
	startSmokeWorkflow: "chromaAgentics.backend.startSmokeWorkflow",
	disconnect: "chromaAgentics.backend.disconnect",
} as const

export type BackendBridgeTestHandle = {
	client: BackendBridgeClient
	logger: BackendBridgeSafeLogger
	statusBar: BackendBridgeStatusBar
	clearToken(): Promise<void>
}

declare global {
	// eslint-disable-next-line no-var
	var __chromaBackendBridgeTestHandle: BackendBridgeTestHandle | undefined
}

export function getBackendBridgeTestHandle(): BackendBridgeTestHandle | undefined {
	return globalThis.__chromaBackendBridgeTestHandle
}

export function registerBackendBridge(context: vscode.ExtensionContext): void {
	const outputChannel = vscode.window.createOutputChannel("Chroma Agentics Backend")
	const logger = new BackendBridgeSafeLogger(outputChannel)
	const statusBar = new BackendBridgeStatusBar()
	const tokenStore = new BackendBridgeTokenStore(context.secrets)
	const client = new BackendBridgeClient({
		readConfig: readBackendBridgeConfig,
		tokenStore,
		logger,
		status: statusBar,
	})
	globalThis.__chromaBackendBridgeTestHandle = {
		client,
		logger,
		statusBar,
		clearToken: () => tokenStore.clearToken(),
	}

	context.subscriptions.push(outputChannel, statusBar)

	context.subscriptions.push(
		vscode.commands.registerCommand(backendBridgeCommandIds.setToken, async (providedToken?: string) => {
			const token =
				typeof providedToken === "string"
					? providedToken
					: await vscode.window.showInputBox({
							prompt: "Enter the Chroma backend development token.",
							password: true,
							ignoreFocusOut: true,
							placeHolder: "Development token",
						})

			if (!token || token.trim().length === 0) {
				logger.info("token update cancelled")
				return
			}

			await tokenStore.setToken(token.trim())
			logger.info("token stored in SecretStorage")
			vscode.window.showInformationMessage("Chroma backend token stored in SecretStorage.")
		}),
		vscode.commands.registerCommand(backendBridgeCommandIds.clearToken, async () => {
			await tokenStore.clearToken()
			logger.info("token cleared from SecretStorage")
			vscode.window.showInformationMessage("Chroma backend token cleared.")
		}),
		vscode.commands.registerCommand(backendBridgeCommandIds.health, async () => {
			outputChannel.show()
			await client.healthCheck()
		}),
		vscode.commands.registerCommand(backendBridgeCommandIds.connect, async () => {
			outputChannel.show()
			await client.connect(true)
		}),
		vscode.commands.registerCommand(backendBridgeCommandIds.startSmokeWorkflow, async () => {
			outputChannel.show()
			await client.startSmokeWorkflow()
		}),
		vscode.commands.registerCommand(backendBridgeCommandIds.disconnect, () => {
			outputChannel.show()
			client.disconnect()
		}),
	)

	const config = readBackendBridgeConfig()
	if (config.enabled) {
		void client.connect(false)
	} else {
		statusBar.setStatus("disabled")
	}
}
