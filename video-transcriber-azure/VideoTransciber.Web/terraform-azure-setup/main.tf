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
  storage_account_name  = azurerm_storage_account.storage.name
  container_access_type = "private"
}