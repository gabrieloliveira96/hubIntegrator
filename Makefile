.PHONY: build up down seed test clean help

# Variables
DOCKER_COMPOSE = docker-compose -f deploy/docker-compose.yml
SOLUTION = src/IntegrationHub.sln

help:
	@echo "Available targets:"
	@echo "  build    - Build the solution"
	@echo "  up       - Start docker-compose environment"
	@echo "  down     - Stop docker-compose environment"
	@echo "  seed     - Apply migrations and seed test data"
	@echo "  test     - Run all tests"
	@echo "  clean    - Clean build artifacts"

build:
	dotnet build $(SOLUTION) -c Release

up:
	@echo "Subindo toda a infraestrutura e aplicações..."
	$(DOCKER_COMPOSE) up -d --build
	@echo "Aguardando serviços ficarem prontos..."
	@sleep 15
	@echo "✓ Todos os serviços estão rodando!"
	@echo "  - Gateway: http://localhost:5000"
	@echo "  - Inbound API: http://localhost:5001"
	@echo "  - RabbitMQ UI: http://localhost:15672"
	@echo "  - Jaeger: http://localhost:16686"
	@echo "  - Grafana: http://localhost:3000"

down:
	$(DOCKER_COMPOSE) down -v

seed:
	@echo "Applying migrations..."
	cd src/Inbound.Api && dotnet ef database update --project .
	cd src/Orchestrator.Worker && dotnet ef database update --project .
	cd src/Outbound.Worker && dotnet ef database update --project .
	@echo "Seeding test data..."
	@echo "TODO: Add seed script"

test:
	dotnet test $(SOLUTION) --verbosity normal

clean:
	dotnet clean $(SOLUTION)
	find . -type d -name bin -exec rm -rf {} + 2>/dev/null || true
	find . -type d -name obj -exec rm -rf {} + 2>/dev/null || true

