//-----------------------------------------------------------------------
// <copyright file="AutoFacDependencyResolver.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2016 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2013-2016 Akka.NET project <https://github.com/akkadotnet/akka.net>
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Runtime.CompilerServices;
using Akka.Actor;
using Akka.DI.Core;
using Autofac;
using Xunit.Abstractions;

namespace Akka.DI.AutoFac
{
    /// <summary>
    /// Provides services to the <see cref="ActorSystem "/> extension system
    /// used to create actors using the AutoFac IoC container.
    /// </summary>
    public class DebugAutoFacDependencyResolver : IDependencyResolver, INoSerializationVerificationNeeded
    {
        private ITestOutputHelper output;
        private ILifetimeScope container;
        private ConcurrentDictionary<string, Type> typeCache;
        private ActorSystem system;
        private ConditionalWeakTable<ActorBase, ILifetimeScope> references;

        /// <summary>
        /// Initializes a new instance of the <see cref="AutoFacDependencyResolver"/> class.
        /// </summary>
        /// <param name="container">The container used to resolve references</param>
        /// <param name="system">The actor system to plug into</param>
        /// <exception cref="ArgumentNullException">
        /// Either the <paramref name="container"/> or the <paramref name="system"/> was null.
        /// </exception>
        public DebugAutoFacDependencyResolver(ITestOutputHelper output, ILifetimeScope container, ActorSystem system)
        {
            this.output = output;
            if (system == null) throw new ArgumentNullException("system");
            if (container == null) throw new ArgumentNullException("container");
            this.container = container;
            typeCache = new ConcurrentDictionary<string, Type>(StringComparer.OrdinalIgnoreCase);
            this.system = system;
            this.system.AddDependencyResolver(this);
            this.references = new ConditionalWeakTable<ActorBase, ILifetimeScope>();
        }

        /// <summary>
        /// Retrieves an actor's type with the specified name
        /// </summary>
        /// <param name="actorName">The name of the actor to retrieve</param>
        /// <returns>The type with the specified actor name</returns>
        public Type GetType(string actorName)
        {
            output.WriteLine($"GetType({actorName})");

            typeCache.
                TryAdd(actorName,
                       actorName.GetTypeValue() ??
                       container.
                       ComponentRegistry.
                       Registrations.
                       Where(registration => registration.Activator.LimitType.
                                 Name.Equals(actorName, StringComparison.OrdinalIgnoreCase)).
                        Select(registration => registration.Activator.LimitType).
                        FirstOrDefault());

            return typeCache[actorName];
        }

        /// <summary>
        /// Creates a delegate factory used to create actors based on their type
        /// </summary>
        /// <param name="actorType">The type of actor that the factory builds</param>
        /// <returns>A delegate factory used to create actors</returns>
        public Func<ActorBase> CreateActorFactory(Type actorType)
        {
            output.WriteLine($"CreateActorFactory({actorType})");
            output.WriteLine(Environment.StackTrace);

            return () =>
            {
                output.WriteLine($"CreateActorFactory({actorType}) anonymous method");
                output.WriteLine(Environment.StackTrace);

                var scope = container.BeginLifetimeScope();
                var actor = (ActorBase)scope.Resolve(actorType);
                references.Add(actor, scope);
                return actor;
            };
        }

        /// <summary>
        /// Used to register the configuration for an actor of the specified type <typeparamref name="TActor"/>
        /// </summary>
        /// <typeparam name="TActor">The type of actor the configuration is based</typeparam>
        /// <returns>The configuration object for the given actor type</returns>
        public Props Create<TActor>() where TActor : ActorBase
        {
            output.WriteLine($"Create<TActor>()");

            return Create(typeof(TActor));
        }

        /// <summary>
        /// Used to register the configuration for an actor of the specified type <paramref name="actorType"/> 
        /// </summary>
        /// <param name="actorType">The <see cref="Type"/> of actor the configuration is based</param>
        /// <returns>The configuration object for the given actor type</returns>
        public virtual Props Create(Type actorType)
        {
            output.WriteLine($"Create({actorType})");

            return system.GetExtension<DIExt>().Props(actorType);
        }

        /// <summary>
        /// Signals the container to release it's reference to the actor.
        /// </summary>
        /// <param name="actor">The actor to remove from the container</param>
        public void Release(ActorBase actor)
        {
            output.WriteLine($"Release(actor)");

            ILifetimeScope scope;

            if (references.TryGetValue(actor, out scope))
            {
                scope.Dispose();
                references.Remove(actor);
            }
        }
    }
}