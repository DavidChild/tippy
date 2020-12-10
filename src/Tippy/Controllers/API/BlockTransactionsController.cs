using System;
using System.Linq;
using Ckb.Rpc;
using Ckb.Types;
using Microsoft.AspNetCore.Mvc;
using Tippy.ApiData;
using Tippy.Util;

namespace Tippy.Controllers.API
{
    [Route("api/v1/block_transactions")]
    [ApiController]
    public class BlockTransactionsController : ApiControllerBase
    {
        [HttpGet("{blockHash}")]
        public ActionResult Index(string blockHash, [FromQuery(Name = "page")] int? page, [FromQuery(Name = "page_size")] int? pageSize)
        {
            Client? client = Rpc();
            if (client == null)
            {
                return NoContent();
            }

            Block? block = client.GetBlock(blockHash);
            if (block == null)
            {
                return NoContent();
            }

            if (page == null || pageSize == null)
            {
                ArrayResult<BlockTransactionResult> txResults = GetTransactions(client, block, 0, 10);
                return Ok(txResults);
            }

            if (page < 1 || pageSize < 1)
            {
                return NoContent();
            }

            int skipCount = ((int)page - 1) * (int)pageSize;

            Meta meta = new();
            meta.Total = (UInt64)block.Transactions.Length;
            meta.PageSize = (int)pageSize;

            ArrayResult<BlockTransactionResult> result = GetTransactions(client, block, skipCount, (int)pageSize);

            return Ok(result);
        }

        private ArrayResult<BlockTransactionResult> GetTransactions(Client client, Block block, int skipCount, int size, Meta? meta = null)
        {
            string prefix = IsMainnet() ? "ckb" : "ckt";
            BlockTransactionResult[] result = block.Transactions.Skip(skipCount).Take(size).Select((tx, i) =>
            {
                string txHash = tx.Hash ?? "0x";
                bool isCellbase = i == 0 && skipCount == 0;
                UInt64 blockNumber = Hex.HexToUInt64(block.Header.Number);

                BlockTransactionResult txResult = new()
                {
                    IsCellbase = isCellbase,
                    TransactionHash = txHash,
                    BlockNumber = blockNumber.ToString(),
                    BlockTimestamp = Hex.HexToUInt64(block.Header.Timestamp).ToString(),
                };

                if (isCellbase)
                {
                    var (displayInputs, displayOutputs) = TransactionsController.GenerateCellbaseDisplayInfos(client, txHash, tx.Outputs, blockNumber, prefix);
                    txResult.DisplayInputs = displayInputs;
                    txResult.DisplayOutputs = displayOutputs;
                }
                else
                {
                    Input[] inputs = tx.Inputs.Take(10).ToArray();
                    Output[] outputs = tx.Outputs.Take(10).ToArray();
                    Output[] previousOutptus = GetPreviousOutputs(client, inputs);
                    var (displayInputs, displayOutputs) = TransactionsController.GenerateNotCellbaseDisplayInfos(inputs, outputs, previousOutptus, prefix);
                    txResult.DisplayInputs = displayInputs;
                    txResult.DisplayOutputs = displayOutputs;
                }
                return txResult;
            }).ToArray();

            return new ArrayResult<BlockTransactionResult>("ckb_transactions", result);
        }

        private static Output[] GetPreviousOutputs(Client client, Input[] inputs)
        {
            return inputs.Select(input => GetPreviousOutput(client, input)).ToArray();
        }

        private static Output GetPreviousOutput(Client client, Input input)
        {
            TransactionWithStatus? txWithStatus = client.GetTransaction(input.PreviousOutput.TxHash);
            if (txWithStatus == null)
            {
                throw new Exception("");
            }
            return txWithStatus.Transaction.Outputs[Hex.HexToUInt32(input.PreviousOutput.Index)];
        }
    }
}
