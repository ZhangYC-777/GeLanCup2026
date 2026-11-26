# Unity MCP connection

Connects this profile to the **MCP for Unity** server that runs inside the
Unity Editor opened on the GeLanCup2026 project.

- Endpoint: `http://127.0.0.1:8080/mcp` (Streamable HTTP)
- Tool namespace: `mcp__unity__*`

## Usage

1. Open the GeLanCup2026 project with Unity 2022.3.62f3.
2. In Unity, open **Window → MCP for Unity → Toggle MCP Window** and
   **Start Server** in HTTP mode on port 8080.
3. Restart the session (or reload this plugin) and confirm the
   `mcp__unity__*` tools appear. A read-only call such as
   `mcp__unity__read_console` or `mcp__unity__get_scene_info` verifies the
   connection targets the currently open project.

## Limitations

- The server only exists while the Unity Editor is running with the MCP
  window server started.
- The connection fails on startup when the server is unreachable
  (`failOnStartupError: true`); start the Unity server before opening the
  session.
