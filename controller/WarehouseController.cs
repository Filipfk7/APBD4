using System.Data.SqlClient;
using APBD4.entity;
using Microsoft.AspNetCore.Mvc;

namespace APBD4.controller;

public class WarehouseController : ControllerBase
{
    private readonly string? _connectionString;

    public WarehouseController(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection");
    }

    [HttpPost("AddProductToWarehouse")]
    public async Task<IActionResult> AddProductToWarehouse([FromBody] ProductWarehouseRequest request)
    {
        using (var connection = new SqlConnection(_connectionString))
        {
            await connection.OpenAsync();
            using (var transaction = connection.BeginTransaction())
            {
                try
                {
                    if (!await ProductAndWarehouseExist(request, connection, transaction))
                    {
                        return BadRequest("Invalid product or warehouse.");
                    }

                    var orderId = await GetValidOrderId(request, connection, transaction);
                    if (orderId == 0)
                    {
                        return BadRequest("No valid order found or order already fulfilled.");
                    }

                    await UpdateOrderAsFulfilled(orderId, connection, transaction);

                    var productWarehouseId = await AddProductToWarehouse(request, orderId, connection, transaction);
                    transaction.Commit();
                    return Ok(new { IdProductWarehouse = productWarehouseId });
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    return BadRequest($"An error occurred: {ex.Message}");
                }
            }
        }
    }

    private async Task<bool> ProductAndWarehouseExist(ProductWarehouseRequest request, SqlConnection connection, SqlTransaction transaction)
    {
        var query = @"
        SELECT 
            (SELECT COUNT(*) FROM Product WHERE IdProduct = @IdProduct) AS ProductExists,
            (SELECT COUNT(*) FROM Warehouse WHERE IdWarehouse = @IdWarehouse) AS WarehouseExists;
    ";

        using (var command = new SqlCommand(query, connection, transaction))
        {
            command.Parameters.AddWithValue("@IdProduct", request.IdProduct);
            command.Parameters.AddWithValue("@IdWarehouse", request.IdWarehouse);
            using (var reader = await command.ExecuteReaderAsync())
            {
                if (reader.Read())
                {
                    bool productExists = (int)reader["ProductExists"] > 0;
                    bool warehouseExists = (int)reader["WarehouseExists"] > 0;
                    return productExists && warehouseExists;
                }
            }
        }
        return false;
    }


    private async Task<int> GetValidOrderId(ProductWarehouseRequest request, SqlConnection connection, SqlTransaction transaction)
    {
        var query = @"SELECT IdOrder FROM [Order] WHERE IdProduct = @IdProduct AND Amount >= @Amount AND CreatedAt <= @RequestCreatedAt AND FulfilledAt IS NULL;";
        using (var command = new SqlCommand(query, connection, transaction))
        {
            command.Parameters.AddWithValue("@IdProduct", request.IdProduct);
            command.Parameters.AddWithValue("@Amount", request.Amount);
            command.Parameters.AddWithValue("@RequestCreatedAt", request.RequestCreatedAt);
            using (var reader = await command.ExecuteReaderAsync())
            {
                if (reader.Read())
                {
                    return reader.GetInt32(0);
                }
                return 0;
            }
        }
    }

    private async Task<int> AddProductToWarehouse(ProductWarehouseRequest request, int orderId, SqlConnection connection, SqlTransaction transaction)
    {
        var query = @"
        INSERT INTO Product_Warehouse (IdWarehouse, IdProduct, IdOrder, Amount, Price, CreatedAt)
        VALUES (@IdWarehouse, @IdProduct, @IdOrder, @Amount, (SELECT Price FROM Product WHERE IdProduct = @IdProduct) * @Amount, GETDATE());
        SELECT SCOPE_IDENTITY();
    ";

        using (var command = new SqlCommand(query, connection, transaction))
        {
            command.Parameters.AddWithValue("@IdWarehouse", request.IdWarehouse);
            command.Parameters.AddWithValue("@IdProduct", request.IdProduct);
            command.Parameters.AddWithValue("@IdOrder", orderId);
            command.Parameters.AddWithValue("@Amount", request.Amount);
            try
            {
                var result = await command.ExecuteScalarAsync();
                return Convert.ToInt32(result);
            }
            catch (SqlException ex)
            {
                transaction.Rollback();
                throw;
            }
        }
    }
    
    private async Task UpdateOrderAsFulfilled(int orderId, SqlConnection connection, SqlTransaction transaction)
    {
        var query = @"UPDATE [Order] SET FulfilledAt = GETDATE() WHERE IdOrder = @IdOrder;";
        using (var command = new SqlCommand(query, connection, transaction))
        {
            command.Parameters.AddWithValue("@IdOrder", orderId);
            await command.ExecuteNonQueryAsync();
        }
    }
}