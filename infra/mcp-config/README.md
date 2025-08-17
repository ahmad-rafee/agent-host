# MCP Server Configuration

This directory contains configuration files for Model Context Protocol (MCP) servers.

## Available MCP Servers

The following MCP servers are installed and available:

### 1. Filesystem Server
- **Package**: `@modelcontextprotocol/server-filesystem`
- **Purpose**: File system operations (read, write, list files)
- **Tools**: `fs.read_file`, `fs.write_file`, `fs.list_files`, `fs.delete_file`, `fs.create_directory`

### 2. Gmail Server (requires credentials)
- **Package**: `@modelcontextprotocol/server-gmail`
- **Purpose**: Gmail integration for reading and sending emails
- **Tools**: `gmail.list_messages`, `gmail.get_message`, `gmail.send_message`, `gmail.search_messages`
- **Configuration**: Requires `gmail-credentials.json` file

### 3. Jira Server (requires configuration)
- **Package**: `@modelcontextprotocol/server-jira`
- **Purpose**: Jira integration for issue management
- **Tools**: `jira.create_issue`, `jira.update_issue`, `jira.get_issue`, `jira.search_issues`, `jira.transition_issue`
- **Configuration**: Requires JIRA_URL, JIRA_USERNAME, JIRA_API_TOKEN environment variables

## Configuration

MCP servers are configured in the application's `appsettings.json` under the `Mcp.Servers` section:

```json
{
  "Mcp": {
    "Servers": {
      "filesystem": {
        "Command": "npx",
        "Args": ["@modelcontextprotocol/server-filesystem", "/app/workspace"]
      },
      "gmail": {
        "Command": "npx",
        "Args": ["@modelcontextprotocol/server-gmail"],
        "Env": {
          "GMAIL_CREDENTIALS_FILE": "/app/config/gmail-credentials.json"
        }
      },
      "jira": {
        "Command": "npx",
        "Args": ["@modelcontextprotocol/server-jira"],
        "Env": {
          "JIRA_URL": "https://your-org.atlassian.net",
          "JIRA_USERNAME": "your-username",
          "JIRA_API_TOKEN": "your-api-token"
        }
      }
    },
    "UseMockResponses": false
  }
}
```

## Development Mode

For development and testing, set `UseMockResponses: true` in the configuration to use mock responses instead of connecting to actual MCP servers.

## Docker Integration

The `mcp-servers` container in `docker-compose.yml` provides the Node.js runtime and installs the MCP server packages. The AgentHost services connect to these servers using stdio transport.
