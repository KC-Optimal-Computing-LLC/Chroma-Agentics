import * as vscode from "vscode"

import { BackendBridgeClient } from "../BackendBridgeClient"
import { backendBridgeCommandIds, registerBackendBridge } from "../registerBackendBridge"

describe("registerBackendBridge", () => {
	afterEach(() => {
		vi.restoreAllMocks()
	})

	it("registers Sprint 3 commands through extension activation without auto-connecting when disabled", () => {
		const registered = new Map<string, (...args: unknown[]) => unknown>()
		vi.spyOn(vscode.workspace, "getConfiguration").mockReturnValue({
			get: (_key: string, defaultValue: unknown) => defaultValue,
		} as any)
		vi.spyOn(vscode.commands, "registerCommand").mockImplementation((command: string, callback: any) => {
			registered.set(command, callback)
			return { dispose: vi.fn() } as any
		})
		const connect = vi.spyOn(BackendBridgeClient.prototype, "connect")

		registerBackendBridge(createContext())

		expect([...registered.keys()].sort()).toEqual(Object.values(backendBridgeCommandIds).sort())
		expect(connect).not.toHaveBeenCalled()
	})

	it("allows explicit backend commands while automatic behavior is disabled", async () => {
		const registered = new Map<string, (...args: unknown[]) => unknown>()
		vi.spyOn(vscode.workspace, "getConfiguration").mockReturnValue({
			get: (_key: string, defaultValue: unknown) => defaultValue,
		} as any)
		vi.spyOn(vscode.commands, "registerCommand").mockImplementation((command: string, callback: any) => {
			registered.set(command, callback)
			return { dispose: vi.fn() } as any
		})
		const healthCheck = vi.spyOn(BackendBridgeClient.prototype, "healthCheck").mockResolvedValue({ ok: true })

		registerBackendBridge(createContext())
		await registered.get(backendBridgeCommandIds.health)?.()

		expect(healthCheck).toHaveBeenCalledTimes(1)
	})

	it("stores a provided token without prompting", async () => {
		const registered = new Map<string, (...args: unknown[]) => unknown>()
		const context = createContext()
		const showInputBox = vi.spyOn(vscode.window, "showInputBox")
		vi.spyOn(vscode.workspace, "getConfiguration").mockReturnValue({
			get: (_key: string, defaultValue: unknown) => defaultValue,
		} as any)
		vi.spyOn(vscode.commands, "registerCommand").mockImplementation((command: string, callback: any) => {
			registered.set(command, callback)
			return { dispose: vi.fn() } as any
		})

		registerBackendBridge(context)
		await registered.get(backendBridgeCommandIds.setToken)?.("provided-token")

		expect(showInputBox).not.toHaveBeenCalled()
		expect(context.secrets.store).toHaveBeenCalledWith("chromaAgentics.backend.devToken", "provided-token")
	})
})

function createContext(): vscode.ExtensionContext {
	return {
		subscriptions: [],
		secrets: {
			get: vi.fn(),
			store: vi.fn(),
			delete: vi.fn(),
		},
	} as unknown as vscode.ExtensionContext
}
