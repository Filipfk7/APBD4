namespace APBD4.entity;

public class ProductWarehouseRequest
{
    public int IdProduct { get; set; }
    public int IdWarehouse { get; set; }
    public int Amount { get; set; }
    public DateTime RequestCreatedAt { get; set; }
}