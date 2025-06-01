provider "azurerm" {
  features {}
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

resource "azurerm_resource_group" "rg" {
  name     = var.resource_group_name
  location = var.location
}

resource "random_string" "suffix" {
  length  = 5
  upper   = false
  number  = true
  special = false
}

resource "azurerm_storage_account" "storage" {
  name                    