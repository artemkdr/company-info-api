#!/bin/bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
OUTPUT_SPEC="$SCRIPT_DIR/config/openapi-enriched.json"
PARAMS_FILE="$SCRIPT_DIR/config/openapi-params.json"

echo "Generating OpenAPI spec for ZAP DAST scanning..."

# Create a minimal but complete OpenAPI 3.0 spec for the CompanyInfo API
cat > "$OUTPUT_SPEC" <<'EOF'
{
  "openapi": "3.0.0",
  "info": {
    "title": "CompanyInfo API",
    "version": "1.0",
    "description": "REST API aggregating European company/VAT/IBAN registries"
  },
  "servers": [
    {
      "url": "http://zap-auth-proxy:80",
      "description": "Test environment (auth-proxy injecting X-Api-Key)"
    }
  ],
  "paths": {
    "/api/v1/health": {
      "get": {
        "summary": "Health check",
        "tags": ["Health"],
        "operationId": "getHealth",
        "responses": {
          "200": {
            "description": "Service is healthy",
            "content": {
              "application/json": {
                "schema": {
                  "type": "object",
                  "properties": {
                    "status": { "type": "string", "example": "Healthy" },
                    "version": { "type": "string", "example": "1.0" }
                  }
                }
              }
            }
          }
        }
      }
    },
    "/api/v1/tva-calculator": {
      "get": {
        "summary": "Calculate TVA/VAT for a SIREN",
        "tags": ["TVA Calculator"],
        "operationId": "calculateTva",
        "parameters": [
          {
            "name": "siren",
            "in": "query",
            "required": true,
            "schema": { "type": "string" },
            "example": "123456789",
            "description": "French company SIREN number"
          }
        ],
        "security": [{ "ApiKey": [] }],
        "responses": {
          "200": {
            "description": "TVA calculation result",
            "content": {
              "application/json": {
                "schema": { "type": "object" }
              }
            }
          },
          "401": { "description": "Unauthorized" },
          "400": { "description": "Invalid SIREN" }
        }
      }
    },
    "/api/v1/iban/verify": {
      "get": {
        "summary": "Verify an IBAN",
        "tags": ["IBAN"],
        "operationId": "verifyIban",
        "parameters": [
          {
            "name": "iban",
            "in": "query",
            "required": true,
            "schema": { "type": "string" },
            "example": "DE89370400440532013000",
            "description": "International Bank Account Number"
          }
        ],
        "security": [{ "ApiKey": [] }],
        "responses": {
          "200": {
            "description": "IBAN validation result",
            "content": { "application/json": { "schema": { "type": "object" } } }
          },
          "401": { "description": "Unauthorized" }
        }
      }
    },
    "/api/v1/vies/validate": {
      "get": {
        "summary": "Validate VAT number via VIES",
        "tags": ["VIES"],
        "operationId": "viesValidate",
        "parameters": [
          {
            "name": "vatNumber",
            "in": "query",
            "required": true,
            "schema": { "type": "string" },
            "example": "FR12345678901",
            "description": "EU VAT number"
          }
        ],
        "security": [{ "ApiKey": [] }],
        "responses": {
          "200": {
            "description": "VIES validation result",
            "content": { "application/json": { "schema": { "type": "object" } } }
          },
          "401": { "description": "Unauthorized" }
        }
      }
    },
    "/api/v1/vies/check-active": {
      "get": {
        "summary": "Check if VAT is active via VIES",
        "tags": ["VIES"],
        "operationId": "viesCheckActive",
        "parameters": [
          {
            "name": "vatNumber",
            "in": "query",
            "required": true,
            "schema": { "type": "string" },
            "example": "FR12345678901"
          }
        ],
        "security": [{ "ApiKey": [] }],
        "responses": {
          "200": {
            "description": "Active status",
            "content": { "application/json": { "schema": { "type": "object" } } }
          },
          "401": { "description": "Unauthorized" }
        }
      }
    },
    "/api/v1/ch-ide/{uid}": {
      "get": {
        "summary": "Get Swiss company data by UID",
        "tags": ["ChIde"],
        "operationId": "getChIde",
        "parameters": [
          {
            "name": "uid",
            "in": "path",
            "required": true,
            "schema": { "type": "string" },
            "example": "CHE123456789"
          }
        ],
        "security": [{ "ApiKey": [] }],
        "responses": {
          "200": {
            "description": "Swiss company data",
            "content": { "application/json": { "schema": { "type": "object" } } }
          },
          "401": { "description": "Unauthorized" }
        }
      }
    },
    "/api/v1/ch-ide/{uid}/validate": {
      "get": {
        "summary": "Validate Swiss UID",
        "tags": ["ChIde"],
        "operationId": "validateChIde",
        "parameters": [
          {
            "name": "uid",
            "in": "path",
            "required": true,
            "schema": { "type": "string" },
            "example": "CHE123456789"
          }
        ],
        "security": [{ "ApiKey": [] }],
        "responses": {
          "200": {
            "description": "Validation result",
            "content": { "application/json": { "schema": { "type": "object" } } }
          },
          "401": { "description": "Unauthorized" }
        }
      }
    },
    "/api/v1/insee/establishments/{siret}": {
      "get": {
        "summary": "Get INSEE establishment data",
        "tags": ["INSEE"],
        "operationId": "getInseeEstablishment",
        "parameters": [
          {
            "name": "siret",
            "in": "path",
            "required": true,
            "schema": { "type": "string" },
            "example": "12345678901234"
          }
        ],
        "security": [{ "ApiKey": [] }],
        "responses": {
          "200": {
            "description": "INSEE establishment data",
            "content": { "application/json": { "schema": { "type": "object" } } }
          },
          "401": { "description": "Unauthorized" }
        }
      }
    },
    "/api/v1/bodacc/search": {
      "get": {
        "summary": "Search BODACC",
        "tags": ["BODACC"],
        "operationId": "searchBodacc",
        "parameters": [
          {
            "name": "companyName",
            "in": "query",
            "schema": { "type": "string" },
            "example": "Test Company"
          },
          {
            "name": "registrationNumber",
            "in": "query",
            "schema": { "type": "string" },
            "example": "123456789"
          }
        ],
        "security": [{ "ApiKey": [] }],
        "responses": {
          "200": {
            "description": "BODACC search results",
            "content": { "application/json": { "schema": { "type": "object" } } }
          },
          "401": { "description": "Unauthorized" }
        }
      }
    }
  },
  "components": {
    "securitySchemes": {
      "ApiKey": {
        "type": "apiKey",
        "in": "header",
        "name": "X-Api-Key",
        "description": "API Key for authentication"
      }
    }
  }
}
EOF

echo "OpenAPI spec generated at $OUTPUT_SPEC"

