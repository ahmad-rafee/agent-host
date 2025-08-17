# Convenience Makefile for common dev tasks

SOLUTION=AgentHost.PoC.sln
CONFIG?=Debug
TEST_RESULTS_DIR=TestResults

.PHONY: restore build test coverage run-api run-worker infra-up infra-down format clean

restore:
	@dotnet restore $(SOLUTION)

build: restore
	@dotnet build $(SOLUTION) -c $(CONFIG) --no-restore

test: build
	@dotnet test $(SOLUTION) -c $(CONFIG) --no-build

coverage: build
	@dotnet test $(SOLUTION) -c $(CONFIG) --no-build \
	  /p:CollectCoverage=true /p:CoverletOutputFormat=opencover /p:CoverletOutput=$(TEST_RESULTS_DIR)/coverage/
	@echo "Coverage reports in $(TEST_RESULTS_DIR)/coverage"

run-api:
	@dotnet run --project src/Api/AgentHost.Api.csproj -c $(CONFIG)

run-worker:
	@dotnet run --project src/Worker/AgentHost.Worker.csproj -c $(CONFIG)

infra-up:
	docker compose -f infra/docker-compose.yml up -d postgres minio litellm

infra-down:
	docker compose -f infra/docker-compose.yml down

format:
	@dotnet format --verify-no-changes || dotnet format

clean:
	@dotnet clean $(SOLUTION)
	@rm -rf $(TEST_RESULTS_DIR)
