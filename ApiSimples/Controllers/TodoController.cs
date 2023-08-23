using ApiSimples.Hubs;
using ApiSimples.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;
using System.Text.Json;

namespace ApiSimples.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TodoController : ControllerBase
    {
        private static List<Item> _itens = new List<Item>();
        private static ConcurrentBag<StreamWriter> _clients = new ConcurrentBag<StreamWriter>();
        private readonly IHubContext<StreamingHub> _streaming;

        public TodoController(IHubContext<StreamingHub> streaming)
        {
            _streaming = streaming;
        }

        [HttpGet]
        public ActionResult<List<Item>> Get() => _itens;

        public async Task<ActionResult<Item>> Post([FromBody] Item value)
        {
            if (value == null)
                return BadRequest();

            if (value.Id == 0)
            {
                var max = _itens.Max(i => i.Id);
                value.Id = max + 1;
            }

            _itens.Add(value);

            await WriteOnStream(value, "Item added");

            return value;
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<Item>> Put(long id, [FromBody] Item value)
        {
            var item = _itens.SingleOrDefault(i => i.Id == id);
            if (item != null)
            {
                _itens.Remove(item);
                value.Id = id;
                _itens.Add(value);

                await WriteOnStream(value, "Item updated");

                return item;
            }

            return BadRequest();
        }

        [HttpDelete("{id}")]
        public async Task<ActionResult> Delete(long id)
        {
            var item = _itens.SingleOrDefault(i => i.Id == id);
            if (item != null)
            {
                _itens.Remove(item);
                await WriteOnStream(item, "Item removed");
                return Ok(new { Description = "Item removed" });
            }

            return BadRequest();
        }

        [HttpGet]
        [Route("streaming")]
        public IActionResult Streaming()
        {
            return new StreamResult(
                (stream, cancelToken) =>
                {
                    var wait = cancelToken.WaitHandle;
                    var client = new StreamWriter(stream);
                    _clients.Add(client);

                    wait.WaitOne();

                    StreamWriter ignore;
                    _clients.TryTake(out ignore);
                },
                HttpContext.RequestAborted);
        }

        //private async Task WriteOnStream(Item data, string action)
        //{
        //    string jsonData = string.Format("{0}\n", JsonSerializer.Serialize(new { data, action }));

        //    foreach (var client in _clients)
        //    {
        //        await client.WriteAsync(jsonData);
        //        await client.FlushAsync();
        //    }
        //}

        //private async Task WriteOnStream(Item data, string action)
        //{
        //    string jsonData = string.Format("{0}\n", JsonSerializer.Serialize(new { data, action }));

        //    //Utiliza o Hub para enviar uma mensagem para ReceiveMessage
        //    await _streaming.Clients.All.SendAsync("ReceiveMessage", jsonData);

        //    foreach (var client in _clients)
        //    {
        //        await client.WriteAsync(jsonData);
        //        await client.FlushAsync();
        //    }
        //}

        private async Task WriteOnStream(Item data, string action)
        {
            foreach (var client in _clients)
            {
                string jsonData = string.Format("{0}\n", JsonSerializer.Serialize(new { data, action }));
                await client.WriteAsync(jsonData);
                await client.FlushAsync();
            }
        }




    }
}
