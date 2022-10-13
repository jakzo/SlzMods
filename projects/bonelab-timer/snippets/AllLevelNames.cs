string.Join(", ", SLZ.Marrow.Warehouse.AssetWarehouse.Instance.GetCrates()
                      .ToArray()
                      .Where(crate => crate.name.StartsWith("Level"))
                      .Select(crate => crate.Title));
