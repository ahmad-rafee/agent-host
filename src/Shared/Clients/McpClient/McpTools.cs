namespace AgentHost.Shared.Clients.McpClient;

/// <summary>
/// Predefined MCP tool names for common integrations
/// These align with the Microsoft MCP SDK tool definitions
/// </summary>
public static class McpTools
{
    public static class Gmail
    {
        public const string ListMessages = "gmail.list_messages";
        public const string GetMessage = "gmail.get_message";
        public const string SendMessage = "gmail.send_message";
        public const string SearchMessages = "gmail.search_messages";
    }

    public static class Jira
    {
        public const string CreateIssue = "jira.create_issue";
        public const string UpdateIssue = "jira.update_issue";
        public const string GetIssue = "jira.get_issue";
        public const string SearchIssues = "jira.search_issues";
        public const string TransitionIssue = "jira.transition_issue";
    }

    public static class Filesystem
    {
        public const string ReadFile = "fs.read_file";
        public const string WriteFile = "fs.write_file";
        public const string ListFiles = "fs.list_files";
        public const string DeleteFile = "fs.delete_file";
        public const string CreateDirectory = "fs.create_directory";
    }

    public static class Slack
    {
        public const string SendMessage = "slack.send_message";
        public const string ListChannels = "slack.list_channels";
        public const string GetMessages = "slack.get_messages";
    }

    public static class GitHub
    {
        public const string CreateIssue = "github.create_issue";
        public const string GetRepository = "github.get_repository";
        public const string CreatePullRequest = "github.create_pull_request";
        public const string ListPullRequests = "github.list_pull_requests";
    }

    /// <summary>
    /// Get all available tool names as a list
    /// </summary>
    public static IEnumerable<string> GetAllTools()
    {
        return typeof(McpTools).GetNestedTypes()
            .SelectMany(type => type.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static))
            .Where(field => field.IsLiteral && !field.IsInitOnly && field.FieldType == typeof(string))
            .Select(field => (string)field.GetRawConstantValue()!)
            .Where(value => !string.IsNullOrEmpty(value));
    }

    /// <summary>
    /// Check if a tool name is a known MCP tool
    /// </summary>
    public static bool IsKnownTool(string toolName)
    {
        return GetAllTools().Contains(toolName, StringComparer.OrdinalIgnoreCase);
    }
}
