import * as vscode from "vscode"

import type { BackendBridgeConnectionState } from "./protocol"

const statusText: Record<BackendBridgeConnectionState, string> = {
	disabled: "$(circle-slash) Chroma Backend",
	disconnected: "$(debug-disconnect) Chroma Backend",
	connecting: "$(sync~spin) Chroma Backend",
	connected: "$(plug) Chroma Backend",
	unhealthy: "$(warning) Chroma Backend",
	"auth failed": "$(lock) Chroma Backend",
	"workflow started": "$(play-circle) Chroma Backend",
	"event received": "$(radio-tower) Chroma Backend",
	"ACK sent": "$(check) Chroma Backend",
	reconnecting: "$(sync~spin) Chroma Backend",
	"resume complete": "$(history) Chroma Backend",
	error: "$(error) Chroma Backend",
}

export class BackendBridgeStatusBar {
	private readonly item: vscode.StatusBarItem
	private currentStatus: BackendBridgeConnectionState = "disabled"
	private currentDetail: string | undefined

	public constructor() {
		this.item = vscode.window.createStatusBarItem(vscode.StatusBarAlignment.Left, 89)
		this.item.command = "chromaAgentics.backend.connect"
		this.setStatus("disabled")
		this.item.show()
	}

	public setStatus(status: BackendBridgeConnectionState, detail?: string): void {
		this.currentStatus = status
		this.currentDetail = detail
		this.item.text = statusText[status]
		this.item.tooltip = detail ? `Chroma backend: ${status} (${detail})` : `Chroma backend: ${status}`
	}

	public getStateForTests(): { status: BackendBridgeConnectionState; detail?: string } {
		return {
			status: this.currentStatus,
			detail: this.currentDetail,
		}
	}

	public dispose(): void {
		this.item.dispose()
	}
}
