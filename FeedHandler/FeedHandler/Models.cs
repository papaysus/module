using System;
using System.Collections.Generic;

namespace Shared
{
    public class SupplierInfo
    {
        public Guid Id { get; set; }
        public int Quantity { get; set; }
        public decimal Price { get; set; }
    }

    public class WarehouseInfo
    {
        public Guid Id { get; set; }
        public int Quantity { get; set; }
        public decimal Price { get; set; }
    }

    public class Product
    {
        public Guid Id { get; set; }
        public string Type { get; set; } = string.Empty;
        public List<SupplierInfo> Suppliers { get; set; } = new();
        public List<Product> SubProducts { get; set; } = new();
        public List<WarehouseInfo> Warehouses { get; set; } = new();
    }

    public class QuantityMessage
    {
        public Guid Id { get; set; }
        public string Type { get; set; } = string.Empty;
        public int WarehouseQuantity { get; set; }
        public int SupplierQuantity { get; set; }
        public int Quantity { get; set; }
        public List<SupplierInfo> Suppliers { get; set; } = new();
        public List<Product> SubProducts { get; set; } = new();
        public List<WarehouseInfo> Warehouses { get; set; } = new();
    }

    public class PriceMessage : QuantityMessage
    {
        public decimal WarehousePrice { get; set; }
        public decimal SupplierPrice { get; set; }
        public decimal MinPrice { get; set; }
    }
}