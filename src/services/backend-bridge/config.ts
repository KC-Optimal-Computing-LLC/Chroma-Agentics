import * as vscode from "vscode"

export const BACKEND_CONFIG_SECTION = "chromaAgentics.backend"

export type BackendBridgeConfig = {
	enabled: boolean
	url: string
	connectionTimeoutMs: number
	reconnect: {
		enabled: boolean
		maxAttempts: number
		initialDelayMs: number
	}
}

export const defaultBackendBridgeConfig: BackendBridgeConfig = {
	enabled: false,
	url: "http://localhost:5127",
	connectionTimeoutMs: 5_000,
	reconnect: {
		enabled: true,
		maxAttempts: 5,
		initialDelayMs: 1_000,
	},
}

export function readBackendBridgeConfig(): BackendBridgeConfig {
	const config = vscode.workspace.getConfiguration(BACKEND_CONFIG_SECTION)

	return {
		enabled: config.get<boolean>("enabled", defaultBackendBridgeConfig.enabled),
		url: normalizeBackendUrl(config.get<string>("url", defaultBackendBridgeConfig.url)),
		connectionTimeoutMs: positiveNumber(
			config.get<number>("connectionTimeoutMs", defaultBackendBridgeConfig.connectionTimeoutMs),
			defaultBackendBridgeConfig.connectionTimeoutMs,
		),
		reconnect: {
			enabled: config.get<boolean>("reconnect.enabled", defaultBackendBridgeConfig.reconnect.enabled),
			maxAttempts: positiveNumber(
				config.get<number>("reconnect.maxAttempts", defaultBackendBridgeConfig.reconnect.maxAttempts),
				defaultBackendBridgeConfig.reconnect.maxAttempts,
			),
			initialDelayMs: positiveNumber(
				config.get<number>("reconnect.initialDelayMs", defaultBackendBridgeConfig.reconnect.initialDelayMs),
				defaultBackendBridgeConfig.reconnect.initialDelayMs,
			),
		},
	}
}

export function normalizeBackendUrl(value: string): string {
	const trimmed = value.trim().replace(/\/+$/, "")
	return trimmed.length > 0 ? trimmed : defaultBackendBridgeConfig.url
}

export function buildHealthUrl(baseUrl: string, path: "live" | "ready" | "dependencies" = "ready"): string {
	return `${normalizeBackendUrl(baseUrl)}/health/${path}`
}

export function buildWebSocketUrl(baseUrl: string): string {
	const parsed = new URL(normalizeBackendUrl(baseUrl))
	if (parsed.protocol === "http:") {
		parsed.protocol = "ws:"
	} else if (parsed.protocol === "https:") {
		parsed.protocol = "wss:"
	} else {
		throw new Error("Backend URL must use http or https.")
	}

	parsed.pathname = joinPath(parsed.pathname, "/ws/events")
	parsed.search = ""
	parsed.hash = ""
	return parsed.toString()
}

function positiveNumber(value: number | undefined, fallback: number): number {
	return typeof value === "number" && Number.isFinite(value) && value > 0 ? value : fallback
}

function joinPath(basePath: string, suffix: string): string {
	const normalizedBase = basePath.replace(/\/+$/, "")
	return `${normalizedBase}${suffix}`
}
