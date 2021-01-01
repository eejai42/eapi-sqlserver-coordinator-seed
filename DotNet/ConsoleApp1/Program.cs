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
            var amqps = "amqps://cli-qbod3_coordinator:test@effortlessapi-rmq.ssot.me/cli-qbod3";
            var crudHandler = new SMQSqlServerCRUDHandler(amqps, "data source=.;initial catalog=cli-qbod3;integrated security=SSPI;", "C:/temp/sql");
            crudHandler.Start();
        }
    }
}
