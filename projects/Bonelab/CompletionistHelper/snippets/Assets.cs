// Title of every asset
var result = "";
foreach (var entry in SLZ.Marrow.Warehouse.AssetWarehouse.Instance
             .InventoryRegistry._entries) {
  if (entry.value == null)
    continue;
  result += entry.value.Title + "\n";
}
result;
