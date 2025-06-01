output "storage_account_name" {
  value = azurerm_storage_account.storage.name
}

output "storage_container_name" {
  value = azurerm_storage_container.container.name
}

output "connection_string" {
  value     = azurerm_storage_account.storage.primary_connection_string
  sensitive = true
}
