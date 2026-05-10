import type * as vscode from "vscode"

export type SafeLogMetadata = {
	backendStatus?: string
	workflowId?: string | null
	sessionId?: string | null
	eventName?: string | null
	sequence?: number | null
	errorCode?: string | null
	summary?: string | null
}

export class BackendBridgeSafeLogger {
	private readonly entries: string[] = []

	public constructor(private readonly outputChannel: vscode.OutputChannel) {}

	public info(message: string, metadata: SafeLogMetadata = {}): void {
		this.write(message, metadata)
	}

	public error(message: string, metadata: SafeLogMetadata = {}): void {
		this.write(message, metadata)
	}

	public show(): void {
		this.outputChannel.show()
	}

	public getEntriesForTests(): readonly string[] {
		return this.entries
	}

	private write(message: string, metadata: SafeLogMetadata): void {
		const line = `[Chroma Backend] ${message}${formatMetadata(metadata)}`
		this.entries.push(line)
		if (this.entries.length > 200) {
			this.entries.shift()
		}
		this.outputChannel.appendLine(line)
	}
}

export function formatMetadata(metadata: SafeLogMetadata): string {
	const entries = Object.entries(metadata)
		.filter(([, value]) => value !== undefined && value !== null && value !== "")
		.map(([key, value]) => `${key}=${sanitizeMetadataValue(String(value))}`)

	return entries.length > 0 ? ` ${entries.join(" ")}` : ""
}

export function sanitizeMetadataValue(value: string): string {
	return value.replace(/[\r\n\t]/g, " ").slice(0, 160)
}
