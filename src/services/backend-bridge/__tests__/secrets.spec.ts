import { BACKEND_DEV_TOKEN_SECRET_KEY, BackendBridgeTokenStore } from "../secrets"

describe("BackendBridgeTokenStore", () => {
	it("stores, replaces, reads, and clears the dev token through SecretStorage", async () => {
		const values = new Map<string, string>()
		const store = new BackendBridgeTokenStore({
			get: vi.fn(async (key: string) => values.get(key)),
			store: vi.fn(async (key: string, value: string) => {
				values.set(key, value)
			}),
			delete: vi.fn(async (key: string) => {
				values.delete(key)
			}),
		} as any)

		await store.setToken("first-token")
		expect(await store.getToken()).toBe("first-token")

		await store.setToken("replacement-token")
		expect(values.get(BACKEND_DEV_TOKEN_SECRET_KEY)).toBe("replacement-token")

		await store.clearToken()
		expect(await store.getToken()).toBeUndefined()
	})
})
