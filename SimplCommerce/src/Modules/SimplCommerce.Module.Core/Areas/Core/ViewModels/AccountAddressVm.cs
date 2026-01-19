namespace SimplCommerce.Module.Core.Areas.Core.ViewModels
{
    public class AccountTaxAndShippingRequestVm
    {
        public AccountShippingAddressVm NewShippingAddress { get; set; }
        public AccountShippingAddressVm NewBillingAddress { get; set; }
        public long? ExistingShippingAddressId { get; set; }
    }

    public class AccountShippingAddressVm
    {
        public long? UserAddressId { get; set; }
        public string ContactName { get; set; }
        public string Phone { get; set; }
        public string AddressLine1 { get; set; }
        public string AddressLine2 { get; set; }
        public long? DistrictId { get; set; }
        public string DistrictName { get; set; }
        public string ZipCode { get; set; }
        public long StateOrProvinceId { get; set; }
        public string StateOrProvinceName { get; set; }
        public string City { get; set; }
        public string CountryId { get; set; }
        public string CountryName { get; set; }
    }
}