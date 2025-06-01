provider "azurerm" {
	features {}
}

resource "azurerm_resource_group" "rg" {
	name     = "transcriber-rg"
	location = "East US"
}

resource "azurerm_storage_account" "storage" {
	name                     = "transcriberstorage"
	resource_group_name      = azurerm_resource_group.rg.name
	location                 = azurerm_resource_group.rg.location
	account_tier             = "Standard"
	account_replication_type = "LRS"
	kind                     = "StorageV2"
}"

resource "random_string" "suffix" {
  length  = 5
  upper   = false
  number  = true
  special = false
}

resource "azurerm_storage_container" "container" {
  name                  = "transcriber-audio"
  storage_account_name  = azurerm_storage_account.storage.name
  container_access_type = "private"
}