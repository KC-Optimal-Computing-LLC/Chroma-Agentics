import * as vscode from "vscode"

import {
	buildHealthUrl,
	buildWebSocketUrl,
	defaultBackendBridgeConfig,
	normalizeBackendUrl,
	readBackendBridgeConfig,
} from "../config"

describe("backend bridge config", () => {
	afterEach(() => {
		vi.restoreAllMocks()
	})

	it("uses Sprint 3 defaults", () => {
		vi.spyOn(vscode.workspace, "getConfiguration").mockReturnValue({
			get: (_key: string, defaultValue: unknown) => defaultValue,
		} as any)

		expect(readBackendBridgeConfig()).toEqual(defaultBackendBridgeConfig)
	})

	it("normalizes backend URLs and builds health URLs", () => {
		expect(normalizeBackendUrl(" http://localhost:5127/// ")).toBe("http://localhost:5127")
		expect(buildHealthUrl("http://localhost:5127/", "dependencies")).toBe(
			"http://localhost:5127/health/dependencies",
		)
	})

	it("builds WebSocket URLs without query token auth", () => {
		expect(buildWebSocketUrl("http://localhost:5127")).toBe("ws://localhost:5127/ws/events")
		expect(buildWebSocketUrl("https://backend.example")).toBe("wss://backend.example/ws/events")
	})
})
