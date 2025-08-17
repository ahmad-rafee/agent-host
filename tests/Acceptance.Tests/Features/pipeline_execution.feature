Feature: Pipeline execution end-to-end
  As a client of AgentHost
  I want to execute a predefined pipeline
  So that I can transform input data and perform actions

  Scenario: Create a run and observe completion policy exposure
    Given the API is running
    And a pipeline named "action-items-from-emails" exists
    When I create a run for pipeline "action-items-from-emails"
    Then the run is created successfully
    When I retrieve the run details
    Then the response includes effective policy information
