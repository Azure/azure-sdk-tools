using System;
using System.Collections.Generic;
using System.Linq;
using APIViewWeb.Repositories;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using System.Threading.Tasks;
using Octokit;
using MongoDB.Driver;
using APIViewWeb.LeanModels;

namespace APIViewWeb.Helpers
{
    public class ChangeHistoryHelpers
    {
        /// <summary>
        /// Given a List of ChangeHistory, and a ChangeAction, update the ChangeHistory with the ChangeAction
        /// depending on the entries already present in the changeHistroy. Return updated ChangeHistory and the ChangeStatus 
        /// which is the overall status of the change Action based on all changes in the changeHistory i.e true if added, false if reverted
        /// Should be used for ChangeActions that are Binary (Added/Reverted) Approved, Delete e.t.c
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <typeparam name="E"></typeparam>
        /// <param name="changeHistory"></param>
        /// <param name="action"></param>
        /// <param name="user"></param>
        /// <param name="notes"></param>
        /// <returns></returns>
        public static (List<T> ChangeHistory, bool ChangeStatus) UpdateBinaryChangeAction<T, E>(List<T> changeHistory, E action, string user, string notes = "")
        {
            var resolvedAction = ResolveAction<E>(action);
            E actionAdded = resolvedAction.actionAdded;
            E actionReverted = resolvedAction.actionReverted;
            bool actionInvalid = resolvedAction.actionInvalid;

            if (actionInvalid)
            {
                throw new ArgumentException($"Invalid arguments action : {action}");
            }            var actionsAddedByUser = GetActionsAdded(changeHistory, actionAdded, user);
            
            T obj = (T)Activator.CreateInstance(typeof(T));

            // Check if this is a one-way action (no revert action)
            if (actionReverted.Equals(default(E)))
            {
                // For one-way actions, always add the action
                obj.GetType().GetProperty("ChangeAction").SetValue(obj, actionAdded);
            }
            else
            {
                // For binary actions, check if we should add or revert
                var actionsRevertedByUser = GetActionsReverted(changeHistory, actionReverted, user);

                if (actionsAddedByUser.Count() > actionsRevertedByUser.Count())
                {
                    obj.GetType().GetProperty("ChangeAction").SetValue(obj, actionReverted);
                }
                else
                {
                    obj.GetType().GetProperty("ChangeAction").SetValue(obj, actionAdded);
                }
            }
            
            obj.GetType().GetProperty("ChangedBy").SetValue(obj, user);
            obj.GetType().GetProperty("Notes").SetValue(obj, notes);
            obj.GetType().GetProperty("ChangedOn").SetValue(obj, DateTime.Now);
            changeHistory.Add(obj);

            // For one-way actions, always return true
            if (actionReverted.Equals(default(E)))
            {
                return (changeHistory, true);
            }

            var actionsAdded = GetActionsAdded(changeHistory, actionAdded);
            var actionsReverted = GetActionsReverted(changeHistory, actionReverted);

            if (actionsAdded.Count() > actionsReverted.Count())
            {
                return (changeHistory, true);
            }
            return (changeHistory, false);
        }

        /// <summary>
        /// Probe the ChangeHistory to figure out the status of the changeAction true for action added, false for action reverted
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <typeparam name="E"></typeparam>
        /// <param name="changeHistory"></param>
        /// <param name="action"></param>
        /// <param name="user"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        public static bool GetChangeActionStatus<T, E>(List<T> changeHistory, E action, string user)
        {
            var resolvedAction = ResolveAction<E>(action);
            E actionAdded = resolvedAction.actionAdded;
            E actionReverted = resolvedAction.actionReverted;
            bool actionInvalid = resolvedAction.actionInvalid;            if (actionInvalid)
            {
                throw new ArgumentException($"Invalid arguments action : {action}");
            }            // For one-way actions, check if the action has been added
            if (actionReverted.Equals(default(E)))
            {
                var actionsAddedByUser = GetActionsAdded(changeHistory, actionAdded, user);
                return actionsAddedByUser.Count() > 0;
            }

            var actionsAddedByUserFinal = GetActionsAdded(changeHistory, actionAdded, user);
            var actionsRevertedByUser = GetActionsReverted(changeHistory, actionReverted, user);
            return (actionsAddedByUserFinal.Count() > actionsRevertedByUser.Count()) ? true : false;
        }
        /// <summary>
        /// From a list of changeHistory get the creator
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="changeHistory"></param>
        /// <returns></returns>
        public static string GetCreator<T>(List<T> changeHistory)
        {
            var creator = changeHistory.Where(c => 
                {
                    var changeAction = c.GetType().GetProperty("ChangeAction").GetValue(c);
                    if (changeAction.ToString().Equals("Created"))
                    {
                        return true;
                    }
                    return false;
                }).First();
            var userProperty = creator.GetType().GetProperty("ChangedBy").GetValue(creator);
            return userProperty.ToString();
        }

        /// <summary>
        /// From a list of changeHistory get the creation date
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="changeHistory"></param>
        /// <returns></returns>
        public static DateTime GetCreationDate<T>(List<T> changeHistory)
        {
            var creator = changeHistory.Where(c =>
            {
                var changeAction = c.GetType().GetProperty("ChangeAction").GetValue(c);
                if (changeAction.ToString().Equals("Created"))
                {
                    return true;
                }
                return false;
            }).First();
            var userProperty = creator.GetType().GetProperty("ChangeDateTime").GetValue(creator);
            return (DateTime)userProperty;
        }

        private static IEnumerable<T1> GetActionsAdded<T1, T2>(List<T1> changeHistory, T2 actionAdded, string user = null)
        {
            return changeHistory.Where(c =>
            {
                var changeAction = c.GetType().GetProperty("ChangeAction").GetValue(c);
                if (String.IsNullOrEmpty(user))
                {
                    return changeAction.Equals(actionAdded);
                }
                var userProperty = c.GetType().GetProperty("ChangedBy").GetValue(c);
                return changeAction.Equals(actionAdded) && userProperty.Equals(user);
            });
        }        private static IEnumerable<T1> GetActionsReverted<T1, T2>(List<T1> changeHistory, T2 actionReverted, string user = null)
        {
            // If actionReverted is null/default, return empty list (for one-way actions)
            if (actionReverted.Equals(default(T2)))
            {
                return new List<T1>();
            }
            
            return changeHistory.Where(c =>
            {
                var changeAction = c.GetType().GetProperty("ChangeAction").GetValue(c);
                if (String.IsNullOrEmpty(user))
                {
                    return changeAction.Equals(actionReverted);
                }
                var userProperty = c.GetType().GetProperty("ChangedBy").GetValue(c);
                return changeAction.Equals(actionReverted) && userProperty.Equals(user);
            });
        }

        private static (E actionAdded, E actionReverted, bool actionInvalid) ResolveAction<E>(E action)
        {
            E actionAdded = default(E);
            E actionReverted = default(E);

            var actionInvalid = false;

            switch (action.ToString())
            {
                case "Approved":
                    Enum.TryParse(typeof(E), "ApprovalReverted", out object ar);
                    actionAdded = action;
                    actionReverted = (E)ar;
                    break;
                case "ApprovalReverted":
                    Enum.TryParse(typeof(E), "Approved", out object a);
                    actionAdded = (E)a;
                    actionReverted = action;
                    break;
                case "Closed":
                    Enum.TryParse(typeof(E), "ReOpened", out object ro);
                    actionAdded = action;
                    actionReverted = (E)ro;
                    break;
                case "ReOpened":
                    Enum.TryParse(typeof(E), "Closed", out object c);
                    actionAdded = (E)c;
                    actionReverted = action;
                    break;
                case "Deleted":
                    Enum.TryParse(typeof(E), "UnDeleted", out object ud);
                    actionAdded = action;
                    actionReverted = (E)ud;
                    break;
                case "UnDeleted":
                    Enum.TryParse(typeof(E), "Deleted", out object d);
                    actionAdded = (E)d;
                    actionReverted = action;
                    break;
                case "Resolved":
                    Enum.TryParse(typeof(E), "UnResolved", out object ur);
                    actionAdded = action;
                    actionReverted = (E)ur;
                    break;                case "UnResolved":
                    Enum.TryParse(typeof(E), "Resolved", out object r);
                    actionAdded = (E)r;
                    actionReverted = action;
                    break;
                case "NamespaceApproved":
                    // This is a one-way action, no revert (only automatic approval)
                    actionAdded = action;
                    actionReverted = default(E);
                    break;
                case "NamespaceReviewRequested":
                    // This is a one-way action, no revert
                    actionAdded = action;
                    actionReverted = default(E);
                    break;
                default:
                    actionInvalid = true;
                    break;
            }
            return (actionAdded, actionReverted, actionInvalid);
        }
    }
}
