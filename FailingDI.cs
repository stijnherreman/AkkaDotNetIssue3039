using Akka.Actor;
using Akka.DI.AutoFac;
using Akka.DI.Core;
using Akka.TestKit.Xunit;
using Autofac;
using Xunit;
using Xunit.Abstractions;

namespace AkkaDotNetIssue3039
{
    public class FailingDI : TestKit
    {
        public FailingDI(ITestOutputHelper output) : base(output: output)
        {
            var containerBuilder = new ContainerBuilder();
            BarActor barActorStub = new BarActorStub();
            containerBuilder.RegisterInstance(barActorStub);
            var container = containerBuilder.Build();
            new DebugAutoFacDependencyResolver(output, container, Sys);
        }

        [Fact]
        public void FailingTest()
        {
            var fooActor = ActorOf(Props.Create<FooActor>());
            fooActor.Tell(new FooActor.Command());
            var message = ExpectMsg<FooActor.CommandResponse>();
            Assert.Equal("stub", message.Content);
        }

        public class FooActor : ReceiveActor
        {
            private IActorRef initialSender;

            public FooActor()
            {
                Receive<Command>(message =>
                {
                    initialSender = Sender;
                    var barActor = Context.ActorOf(Context.DI().Props<BarActor>());
                    barActor.Tell(new BarActor.Query());
                });

                Receive<BarActor.QueryResponse>(message => initialSender.Tell(new CommandResponse(message.Content)));
            }

            public class Command { }

            public class CommandResponse
            {
                public string Content { get; }
                public CommandResponse(string content)
                {
                    Content = content;
                }
            }
        }

        public class BarActor : ReceiveActor
        {
            public BarActor()
            {
                Receive<Query>(message => Sender.Tell(new QueryResponse("real")));
            }

            public class Query { }

            public class QueryResponse
            {
                public string Content { get; }
                public QueryResponse(string content)
                {
                    Content = content;
                }
            }
        }

        public class BarActorStub : BarActor
        {
            public BarActorStub()
            {
                Become(() =>
                {
                    Receive<Query>(message => Sender.Tell(new QueryResponse("stub")));
                });
            }
        }
    }
}