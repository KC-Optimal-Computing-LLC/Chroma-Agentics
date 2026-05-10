import { readFileSync, readdirSync, statSync } from "node:fs"
import { join } from "node:path"

describe("backend bridge scope boundary", () => {
	it("does not call file edit, terminal execution, MCP, approval, tool, dashboard, or webview paths", () => {
		const bridgeRoot = join(__dirname, "..")
		const source = readSourceFiles(bridgeRoot).join("\n")

		for (const forbidden of [
			"workspace.fs",
			"applyEdit",
			"createTerminal",
			"sendText",
			"McpServerManager",
			"executeCommandTool",
			"approval",
			"Webview",
			"webview",
		]) {
			expect(source).not.toContain(forbidden)
		}
	})
})

function readSourceFiles(directory: string): string[] {
	return readdirSync(directory).flatMap((entry) => {
		const fullPath = join(directory, entry)
		if (entry === "__tests__") {
			return []
		}

		if (statSync(fullPath).isDirectory()) {
			return readSourceFiles(fullPath)
		}

		return fullPath.endsWith(".ts") ? [readFileSync(fullPath, "utf8")] : []
	})
}
