using Microsoft.AspNetCore.Mvc;
using XYZ.Billing.Application.Orders;

namespace XYZ.Billing.Api.Controllers;

[ApiController]
[Route("api/orders")]
public class OrdersController(IOrderService orderService) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType<PaymentReceipt>(StatusCodes.Status201Created)]
    [ProducesResponseType<PaymentReceipt>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status402PaymentRequired)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> SubmitOrder([FromBody] OrderRequest orderRequest)
    {
        OrderSubmissionResult result = await orderService.SubmitOrderAsync(orderRequest);
        
        if (result.IsDuplicate)
        {
            return Ok(result.Receipt);
        }
        
        return CreatedAtAction(nameof(GetReceipt), new { orderNumber = result.Receipt.OrderNumber }, result.Receipt);
    }
    
    [HttpGet("{orderNumber}")]
    [ProducesResponseType<PaymentReceipt>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<PaymentReceipt>> GetReceipt(string orderNumber)
    {
        return Ok(await orderService.GetPaymentReceiptAsync(orderNumber));
    }
}