import type { RooCodeAPI } from "@roo-code/types"

type BackendBridgeTestSession = {
	workspaceId: string
	workflowId: string
	sessionId: string
	lastSeenSequence: number
	processedMessageIds: Set<string>
	connectionState: string
	lastErrorCode?: string
}

type BackendBridgeTestHandle = {
	client: {
		getSessionForTests(): BackendBridgeTestSession | undefined
	}
	logger: {
		getEntriesForTests(): readonly string[]
	}
	statusBar: {
		getStateForTests(): { status: string; detail?: string }
	}
	clearToken(): Promise<void>
}

declare global {
	// eslint-disable-next-line no-var
	var api: RooCodeAPI
	// eslint-disable-next-line no-var
	var __chromaBackendBridgeTestHandle: BackendBridgeTestHandle | undefined
}

export {}
