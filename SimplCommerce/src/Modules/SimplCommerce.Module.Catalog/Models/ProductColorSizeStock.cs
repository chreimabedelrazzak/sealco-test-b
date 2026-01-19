using System;
using SimplCommerce.Infrastructure.Models;

namespace SimplCommerce.Module.Catalog.Models
{
    public class ProductColorSizeStock : EntityBaseWithTypedId<long>
    {
        public long ProductId { get; set; }
        public Product Product { get; set; }

        public long? SizeCategoryUnitValueId { get; set; }

        public long? ColorId { get; set; }

        public string Warehouse { get; set; }

        public bool IsSold { get; set; }

        public string Barcode { get; set; }

        public string Supplier { get; set; }

        public int? DeliveryDay { get; set; }

        public string GenerateBarcode { get; set; }

        public decimal? Weight { get; set; }

        public string SpecificLocation { get; set; }

        public bool IsReturnable { get; set; }

        public string CompanyBarcode { get; set; }

        public string Sku { get; set; }

        public string ImgSrc { get; set; }

        public string Img_Src { get; set; }

        public string ImgSrcTexture { get; set; }

        public string ImgSrcHover { get; set; }

        public int Quantity { get; set; }

        public int SoldQuantity { get; set; }

        public int ReservedQuantity { get; set; }

        public bool IsReserved { get; set; }

        public decimal? Price { get; set; }

        public decimal? NewPrice { get; set; }

        public DateTimeOffset CreatedOn { get; set; }

        public DateTimeOffset ModifiedOn { get; set; }

        public bool IsDeleted { get; set; }
    }
}