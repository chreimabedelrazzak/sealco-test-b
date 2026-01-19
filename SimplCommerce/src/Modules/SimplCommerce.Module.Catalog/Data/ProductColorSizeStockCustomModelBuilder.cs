using Microsoft.EntityFrameworkCore;
using SimplCommerce.Infrastructure.Data;
using SimplCommerce.Module.Catalog.Models;

namespace SimplCommerce.Module.Catalog.Data
{
    public class ProductColorSizeStockCustomModelBuilder : ICustomModelBuilder
    {
        public void Build(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ProductColorSizeStock>(b =>
            {
                b.ToTable("Catalog_ProductColorSizeStock");
                b.HasKey(x => x.Id);
                
                // Define the relationship with Product
                b.HasOne(x => x.Product)
                 .WithMany()
                 .HasForeignKey(x => x.ProductId)
                 .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}