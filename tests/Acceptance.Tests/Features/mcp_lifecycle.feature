Feature: MCP server lifecycle and tool synchronization
  As an operator
  I want to manage MCP servers and their tools
  So that discovered tools are available and auditable

  Background:
    Given the API is running

  Scenario: Create an MCP server
    When I create an MCP server named "stub-server" of type "stub"
    Then the MCP server is created successfully

  Scenario: Upsert tools for a server
    Given an MCP server exists named "stub-upsert" of type "stub"
    When I upsert tools on server "stub-upsert":
      | name  | description    |
      | echo  | Echo tool one  |
      | ping  | Ping tool two  |
    Then the server "stub-upsert" lists at least 2 tools

  Scenario: Refresh tools for a server
    Given an MCP server exists named "stub-refresh" of type "stub"
    When I refresh tools for server "stub-refresh"
    Then the server "stub-refresh" lists at least 1 tools

  Scenario: Audit log after refresh
    Given an MCP server exists named "stub-audit" of type "stub"
    When I refresh tools for server "stub-audit"
    Then an audit log exists for server "stub-audit"
