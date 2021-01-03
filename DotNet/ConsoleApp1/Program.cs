using EffortlessApi.SassyMQ.Lib;
using EffortlessAPI.CRUD;
using Newtonsoft.Json;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ConsoleApp1
{
    class Program
    {
        static void Main(string[] args)
        {
            var amqps = "amqp://coordinator:test@localhost/ej-tictactoe-demo";
            var crudHandler = new SMQSqlServerCRUDHandler(amqps, "data source=.;initial catalog=ejtictactoedemo;integrated security=SSPI;", "C:/EAPISqlHostedCore/ej-tictactoe-demo");
            crudHandler.Start();
        }
    }
}
