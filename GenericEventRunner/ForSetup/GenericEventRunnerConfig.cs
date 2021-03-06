﻿// Copyright (c) 2019 Jon P Smith, GitHub: JonPSmith, web: http://www.thereformedprogrammer.net/
// Licensed under MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.EntityFrameworkCore;
using StatusGeneric;

namespace GenericEventRunner.ForSetup
{
    /// <summary>
    /// This holds the configuration settings for the GenericEventRunner
    /// NOTE: This is registered as a singleton, i.e. the values cannot be changes dynamically
    /// </summary>
    public class GenericEventRunnerConfig : IGenericEventRunnerConfig
    {
        private readonly List<(Type dbContextType, Action<DbContext> action)> _actionsToRunAfterDetectChanges 
            = new List<(Type dbContextType, Action<DbContext> action)>();
        private readonly Dictionary<Type, Func<Exception, DbContext, IStatusGeneric>> _exceptionHandlerDictionary
            = new Dictionary<Type, Func<Exception, DbContext, IStatusGeneric>>();

        /// <summary>
        /// This limits the number of times it will look for new events from the BeforeSave events.
        /// This stops circular sets of events
        /// The event runner will throw an exception if the BeforeSave loop goes round move than this number.
        /// </summary>
        public int MaxTimesToLookForBeforeEvents { get; set; } = 6;

        /// <summary>
        /// If this is set to true, then the DuringSave event handlers aren't used
        /// NOTE: This is set to true if the RegisterGenericEventRunner doesn't find any DuringSave event handlers
        /// </summary>
        public bool NotUsingDuringSaveHandlers { get; set; }

        /// <summary>
        /// If this is set to true, then the AfterSave event handlers aren't used
        /// NOTE: This is set to true if the RegisterGenericEventRunner doesn't find any AfterSave event handlers
        /// </summary>
        public bool NotUsingAfterSaveHandlers { get; set; }

        /// <summary>
        /// If true (which is the default value) then the first BeforeSave event handler that returns an error will stop the event runner.
        /// The use cases for each setting is:
        /// true:  Once you have a error, then its not worth going on so stopping quickly is good.
        /// false: If your events have a lot of different checks then this setting gets all the possible errors.
        /// NOTE: Because this is very event-specific you can override this on a per-handler basis via the EventHandlerConfig Attribute
        /// </summary>
        public bool StopOnFirstBeforeHandlerThatHasAnError { get; set; } = true;

        /// <summary>
        /// Add a method which should be executed after ChangeTracker.DetectChanges() has been run for the given DbContext
        /// This is useful to add code that uses the State of entities to  
        /// NOTES:
        /// - DetectChanges won't be called again, so you must ensure that an changes must be manually applied. 
        /// - The BeforeSaveEvents will be run before this action is called
        /// </summary>
        /// <typeparam name="TContext">Must be a DbContext that uses the GenericEventRunner</typeparam>
        /// <param name="runAfterDetectChanges"></param>
        public void AddActionToRunAfterDetectChanges<TContext>(Action<DbContext> runAfterDetectChanges)  where TContext : DbContext
        {
            _actionsToRunAfterDetectChanges.Add((dbContextType: typeof(TContext),  action: runAfterDetectChanges));
        }

        /// <summary>
        /// This holds the list of actions to be run after DetectChanges is called, but before SaveChanges is called
        /// NOTE: The BeforeSaveEvents will be run before these actions
        /// </summary>
        public IReadOnlyList<(Type dbContextType, Action<DbContext> action)> ActionsToRunAfterDetectChanges =>
            _actionsToRunAfterDetectChanges.AsReadOnly();

        /// <summary>
        /// This method allows you to register an exception handler for a specific DbContext type
        /// When SaveChangesWithValidation is called if there is an exception then this method is called (if present)
        /// a) If it returns null then the error is rethrown. This means the exception handler can't handle that exception.
        /// b) If it returns a status with errors then those are combined into the GenericEventRunner status.
        /// c) If it returns a valid status (i.e. no errors) then it calls SaveChanges again, still with exception capture.
        /// Item b) is useful for turning SQL errors into user-friendly error message, and c) is good for handling a DbUpdateConcurrencyException
        /// </summary>
        public void RegisterSaveChangesExceptionHandler<TContext>(
            Func<Exception, DbContext, IStatusGeneric> exceptionHandler) where TContext : DbContext
        {
            if (_exceptionHandlerDictionary.ContainsKey(typeof(TContext)))
                throw new InvalidOperationException(
                    $"You can only register one exception handler per DbContext type. You all ready have registered {typeof(TContext).Name}");
            _exceptionHandlerDictionary[typeof(TContext)] = exceptionHandler;
        }

        /// <summary>
        /// This holds the Dictionary of exception handlers for a specific DbContext
        /// </summary>
        public ImmutableDictionary<Type, Func<Exception, DbContext, IStatusGeneric>> ExceptionHandlerDictionary =>
            _exceptionHandlerDictionary.ToImmutableDictionary();
    }
}