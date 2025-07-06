provider "azurerm" {
  features {}
  subscription_id = "e89ec99d-4196-49df-aa73-e842e9574661"
}

variable "resource_group_name" {
  description = "Name of the resource group"
  type        = string
  default     = "transcriber-rg"
}

variable "location" {
  description = "Azure region"
  type        = string
  default     = "East US"
}

variable "storage_container_name" {
  description = "Name of the storage container"
  type        = string
  default     = "transcriber-audio"
}

resource "random_string" "suffix" {
  length  = 5
  upper   = false
  numeric = true
  special = false
}

resource "azurerm_resource_group" "rg" {
  name     = var.resource_group_name
  location = var.location
}

resource "azurerm_storage_account" "storage" {
  name                     = "transcriber${random_string.suffix.result}"
  resource_group_name      = azurerm_resource_group.rg.name
  location                 = azurerm_resource_group.rg.location
  account_tier             = "Standard"
  account_replication_type = "LRS"
}

resource "azurerm_storage_container" "container" {
  name                  = var.storage_container_name
  storage_account_id    = azurerm_storage_account.storage.id
  container_access_type = "private"
}

# Create the App Service Plan (Consumption Plan for Function App)
resource "azurerm_app_service_plan" "function_plan" {
  name                = "transcriber-func-plan"
  location            = azurerm_resource_group.rg.location
  resource_group_name = azurerm_resource_group.rg.name
  kind                = "FunctionApp"
  reserved            = true  # Required for Linux plans

  sku {
    tier = "Dynamic"
    size = "Y1"
  }
}

# Create the Function App
resource "azurerm_linux_function_app" "function_app" {
  name                       = "transcriber-func-${random_string.suffix.result}"
  resource_group_name        = azurerm_resource_group.rg.name
  location                   = azurerm_resource_group.rg.location
  service_plan_id            = azurerm_app_service_plan.function_plan.id
  storage_account_access_key = azurerm_storage_account.storage.primary_access_key

  site_config {
    application_stack {
      python_version = "3.10"
    }
  }

  app_settings = {
    "AzureWebJobsStorage" = azurerm_storage_account.storage.primary_connection_string
    "FUNCTIONS_WORKER_RUNTIME" = "python"
  }

  https_only = true
}
