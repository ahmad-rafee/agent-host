Feature: Policy scopes management
  As an operator
  I want to extend and manage run-scoped policy allowances
  So that I can dynamically permit additional models and tools

  Background:
    Given the API is running
    And a pipeline named "action-items-from-emails" exists
    When I create a run for pipeline "action-items-from-emails"
    Then the run is created successfully

  Scenario: Add a new model scope and observe effective models
    When I add a policy scope with model "new-model-1"
    And I retrieve the run details
    Then the effective models include "new-model-1"

  Scenario: Add a tool scope and list scopes
    When I add a policy scope with tool "echo"
    And I list policy scopes
  Then at least 1 policy scopes are returned

  Scenario: Delete a policy scope
    When I add a policy scope with model "temp-model"
    And I list policy scopes
    And I delete the last added policy scope
    And I list policy scopes
    Then the effective models do not include "temp-model"
