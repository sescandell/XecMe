﻿using System;
using System.Collections.Generic;
using XecMe.Common;
using XecMe.Common.Injection;

namespace XecMe.Core.Injection
{
    /// <summary>
    /// This is the default implementation if <see cref="IContainer"/>
    /// </summary>
    /// <seealso cref="XecMe.Common.Injection.IContainer" />
    public class DefaultContainer : IContainer
    {
        /// <summary>
        /// Begins the scope of instances
        /// </summary>
        /// <returns></returns>
        public IDisposable BeginScope()
        {
            return null;
        }

        /// <summary>
        /// Gets the collection of the type from the underlying container
        /// </summary>
        /// <typeparam name="TType">The type of the type.</typeparam>
        /// <returns></returns>
        public IEnumerable<TType> GetCollection<TType>() where TType : class
        {
            return null;
        }

        /// <summary>
        /// Gets the instance of the type requested form the container
        /// </summary>
        /// <typeparam name="TType">The type of the type.</typeparam>
        /// <returns></returns>
        public TType GetInstance<TType>() where TType : class
        {
            return Reflection.CreateInstance<TType>();
        }

        /// <summary>
        /// Gets the instance.
        /// </summary>
        /// <param name="serviceType">Type of the service.</param>
        /// <returns></returns>
        public object GetInstance(Type serviceType)
        {
            return Reflection.CreateInstance(serviceType);
        }

        /// <summary>
        /// Registers the specified service type into the underlying container
        /// </summary>
        /// <param name="serviceType">Type of the service.</param>
        public void Register(Type serviceType)
        {
        }

        /// <summary>
        /// Registers this instance.
        /// </summary>
        /// <typeparam name="TService">The type of the service.</typeparam>
        /// <typeparam name="TImplementation">The type of the implementation.</typeparam>
        void IContainer.Register<TService, TImplementation>()
        {
        }
    }
}
