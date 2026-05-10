import type * as vscode from "vscode"

export const BACKEND_DEV_TOKEN_SECRET_KEY = "chromaAgentics.backend.devToken"

export class BackendBridgeTokenStore {
	public constructor(private readonly secrets: vscode.SecretStorage) {}

	public async getToken(): Promise<string | undefined> {
		const token = await this.secrets.get(BACKEND_DEV_TOKEN_SECRET_KEY)
		return token && token.trim().length > 0 ? token : undefined
	}

	public async setToken(token: string): Promise<void> {
		await this.secrets.store(BACKEND_DEV_TOKEN_SECRET_KEY, token)
	}

	public async clearToken(): Promise<void> {
		await this.secrets.delete(BACKEND_DEV_TOKEN_SECRET_KEY)
	}
}
