﻿//******************************************************************************
// <copyright file="license.md" company="RawCMS project  (https://github.com/arduosoft/RawCMS)">
// Copyright (c) 2019 RawCMS project  (https://github.com/arduosoft/RawCMS)
// RawCMS project is released under GPL3 terms, see LICENSE file on repository root at  https://github.com/arduosoft/RawCMS .
// </copyright>
// <author>Daniele Fontani, Emanuele Bucarelli, Francesco Min�</author>
// <autogenerated>true</autogenerated>
//******************************************************************************
using Microsoft.Extensions.Logging;
using RawCMS.Library.Core.Extension;
using RawCMS.Library.Core.Interfaces;
using RawCMS.Library.Service;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace RawCMS.Library.Core
{
    public class AppEngine
    {
        //#region singleton
        //private static LambdaManager _current = null;
        private static ILogger _logger;

        private ILoggerFactory loggerFactory;

        public ILogger GetLogger(object caller)
        {
            return loggerFactory.CreateLogger(caller.GetType());
        }

        //public static void SetLogger(ILoggerFactory factory)
        //{
        //    logger = factory.CreateLogger(typeof(LambdaManager));
        //}
        //public static LambdaManager Current
        //{
        //    get
        //    {
        //        return _current ?? (_current= new LambdaManager() );
        //    }
        //}
        //#endregion

        public CRUDService Service { get => service; set { service = value; service.SetAppEngine(this); } }

        public List<Lambda> Lambdas { get; set; } = new List<Lambda>();
        //public Lambda this[string name]
        //{
        //    get
        //    {
        //        return Lambdas.FirstOrDefault(x => x.Name == name);
        //    }
        //}

        public List<Plugin> Plugins { get; set; } = new List<Plugin>();
        //public Plugin this[string name]
        //{
        //    get
        //    {
        //        return Plugin.FirstOrDefault(x => x.Name == name);
        //    }
        //}

        public AppEngine(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger(typeof(AppEngine));
            this.loggerFactory = loggerFactory;

            LoadAllAssembly();
            LoadPlugins();
        }

        public void Init()
        {
            LoadLambdas();
        }

        private void LoadPlugins()
        {
            _logger.LogDebug("Load plugins");

            Plugins = GetAnnotatedInstances<Plugin>();

            if (_logger.IsEnabled(LogLevel.Debug))
            {
                Plugins.ForEach(x =>
                {
                    _logger.LogDebug("Plugin enabled {0}", x.Name);
                });
            }
            Plugins.ForEach(x => x.SetAppEngine(this));

            //Core plugin must be the first to be called. This ensure it also in case thirdy party define malicius priority.
            int minPriority = 0;
            Plugins.ForEach(x => { if (x.Priority <= minPriority) { minPriority = x.Priority - 1; } });
            Plugin corePlugin = Plugins.Single(x => x.Name == "Core");
            corePlugin.Priority = minPriority;
        }

        private CRUDService service;

        private void LoadLambdas()
        {
            DiscoverLambdasInBundle();
        }

        public List<string> GetAllAssembly()
        {
            _logger.LogDebug("Get all assembly");
            List<string> dlls = new List<string>();
            // dlls.AddRange(Directory.GetFiles(".\\bin", "*.dll", SearchOption.AllDirectories));

            if (_logger.IsEnabled(LogLevel.Debug))
            {
                dlls.ForEach(x =>
                {
                    _logger.LogDebug("Plugin enabled {0}", x);
                });
            }
            return dlls;
        }

        public void LoadAllAssembly()
        {
            //foreach (var type in Assembly.GetExecutingAssembly().GetTypes())
            //{
            //    foreach (var method in type.GetMethods(BindingFlags.DeclaredOnly |
            //                        BindingFlags.NonPublic |
            //                        BindingFlags.Public | BindingFlags.Instance |
            //                        BindingFlags.Static))
            //    {
            //        System.Runtime.CompilerServices.RuntimeHelpers.PrepareMethod(method.MethodHandle);
            //    }
            //}

            GetAllAssembly().ForEach(x => Assembly.LoadFrom(x));
        }

        public T GetInstance<T>(params object[] args) where T : class
        {
            return Activator.CreateInstance(typeof(T), args) as T;
        }

        public T GetInstance<T>(Type type, params object[] args) where T : class
        {
            return Activator.CreateInstance(type, args) as T;
        }

        public List<Type> GetAnnotatedBy<T>() where T : Attribute
        {
            _logger.LogDebug("Get all entries annotated by {0}", typeof(T).FullName);
            List<Type> result = new List<Type>();
            List<Assembly> bundledAssemblies = GetAssemblyInScope();

            foreach (Assembly assembly in bundledAssemblies)
            {
                _logger.LogDebug("loading from" + assembly.FullName);

                Type[] types = assembly.GetTypes();

                foreach (Type type in types)
                {
                    if (type.GetCustomAttributes(typeof(T), true).Length > 0)
                    {
                        result.Add(type);
                    }
                }
            }
            return result;
        }

        /// <summary>
        /// Get  instances of all classes assignable from T
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public List<T> GetAssignablesInstances<T>() where T : class
        {
            List<Type> types = GetImplementors<T>();
            return GetInstancesFromTypes<T>(types);
        }

        /// <summary>
        /// Get instanced of all classes annotated by T
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public List<T> GetAnnotatedInstances<T>() where T : class
        {
            List<Type> types = GetImplementors<T>();
            return GetInstancesFromTypes<T>(types);
        }

        /// <summary>
        /// Get all types that implements T or inherit it
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public List<Type> GetImplementors<T>() where T : class
        {
            return GetImplementors(typeof(T), GetAssemblyInScope());
        }

        private List<Type> GetImplementors(Type t, List<Assembly> bundledAssemblies)
        {
            _logger.LogDebug("Get implementors for {0} in {1}", t,
                string.Join(",", bundledAssemblies.Select(x => x.FullName).ToArray()));

            List<Type> result = new List<Type>();

            foreach (Assembly assembly in bundledAssemblies)
            {
                _logger.LogDebug("loading from" + assembly.FullName);
                Type[] types = assembly.GetTypes();

                foreach (Type type in types)
                {
                    try
                    {
                        if (t.IsAssignableFrom(type) && !type.IsAbstract && !type.IsInterface)
                        {
                            result.Add(type);
                        }
                    }
                    catch (Exception err)
                    {
                        _logger.LogError(err, "- (unable to create an instance for EXCEPTION skipped) - " + type.Name + " | " + type.GetType().FullName);
                    }
                }
            }
            return result;
        }

        /// <summary>
        /// Get all assemblies that may contains T instances
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public List<Assembly> GetAssemblyInScope()
        {
            List<Assembly> plugins = new List<Assembly>();
            plugins.AddRange(GetAssemblyWithInstance<Plugin>());
            plugins.Add(Assembly.GetExecutingAssembly());
            plugins.Add(Assembly.GetEntryAssembly());
            return plugins;
        }

        public List<Assembly> GetAssemblyWithInstance<T>()
        {
            _logger.LogDebug("Get all assembly with instance");

            List<Assembly> result = new List<Assembly>();
            Assembly[] assList = AppDomain.CurrentDomain.GetAssemblies();
            foreach (Assembly ass in assList)
            {
                List<Type> implementors = GetImplementors(typeof(T), new List<Assembly>() { ass });
                if (implementors.Count > 0)
                {
                    result.Add(ass);
                }
            }
            return result;
        }

        /// <summary>
        /// give instances of a list of types
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="types"></param>
        /// <returns></returns>
        public List<T> GetInstancesFromTypes<T>(List<Type> types) where T : class
        {
            List<T> result = new List<T>();

            types.ForEach(x =>
            {
                result.Add(GetInstance<T>(x));
            });

            return result;
        }

        /// <summary>
        /// Find and load all lambas already loaded with main bundle (no dinamycs)
        /// </summary>
        private void DiscoverLambdasInBundle()
        {
            _logger.LogDebug("Discover Lambdas in Bundle");

            List<Lambda> lambdas = GetAnnotatedInstances<Lambda>();

            foreach (Lambda instance in lambdas)
            {
                if (instance != null)
                {
                    if (instance is IRequireApp)
                    {
                        ((IRequireApp)instance).SetAppEngine(this);
                    }

                    if (instance is IRequireCrudService)
                    {
                        ((IRequireCrudService)instance).SetCRUDService(Service);
                    }

                    if (instance is IInitable)
                    {
                        ((IInitable)instance).Init();
                    }

                    Lambdas.Add(instance);

                    _logger.LogInformation("-" + instance.Name + " | " + instance.GetType().FullName);
                }
            }
        }
    }
}