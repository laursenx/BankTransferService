using BankTransferService.Interfaces;
using BankTransferService.Models;
using Microsoft.AspNetCore.Mvc;

namespace BankTransferService.Controllers;

/// <summary>
/// Handles money transfer requests.
/// </summary>
[ApiController]
[Route("api/transfers")]
public class TransfersController : ControllerBase
{
    private readonly ITransferService _transferService;

    public TransfersController(ITransferService transferService)
    {
        _transferService = transferService;
    }

    /// <summary>
    /// Executes a money transfer between two accounts.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> CreateTransfer([FromBody] TransferRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var result = await _transferService.ExecuteTransferAsync(request);

        if (result.Success)
            return StatusCode(StatusCodes.Status201Created, new { transferId = result.TransferId });

        if (result.IsNotFound)
            return NotFound(new { message = result.ErrorMessage });

        if (result.IsServerError)
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new { message = result.ErrorMessage }
            );

        return BadRequest(new { message = result.ErrorMessage });
    }
}
