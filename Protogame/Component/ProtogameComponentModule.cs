﻿using System;
using System.Linq;
using Protoinject;
using System.Collections.Generic;

#if FALSE
namespace Protogame
{
    public class ProtogameComponentModule : IProtoinjectModule
    {
        public void Load(IKernel kernel)
        {
            kernel.Bind<IBuiltinComponentFactory>().ToFactory();
            kernel.Bind<IEntityFactory>().To<DefaultEntityFactory>();

            kernel.Bind(typeof(IInstantiateComponent<>)).ToMethod(InstantiateComponent);
            kernel.Bind(typeof(IRequireComponent<>)).ToMethod(arg => RequireComponent(arg, ComponentHierarchyPlannerDescendantMode.Immediate));
            kernel.Bind(typeof(IRequireComponentInDescendants<>)).ToMethod(arg => RequireComponent(arg, ComponentHierarchyPlannerDescendantMode.Full));
            kernel.Bind(typeof(IRequireComponentInHierarchy<>)).ToMethod(arg => RequireComponent(arg, ComponentHierarchyPlannerDescendantMode.Full));
        }

        private object InstantiateComponent(IContext arg)
        {
            var svc = arg.Request.Service.GetGenericArguments()[0];
            var parameter = arg.Request.Parameters.OfType<IComponentHierarchyParameter>().First();
            var child = arg.Request.CreateChild(svc, arg, arg.Request.Target);
            var concrete = (IInternalHasComponent)arg.Kernel.Get(typeof(DefaultInstantiateComponent<>).MakeGenericType(svc));
            concrete.Component = arg.Kernel.Resolve(child).First();
            parameter.AddComponentAtPath(GetContextPath(arg), concrete.Component);
            return concrete;
        }

        private object RequireComponent(IContext arg, ComponentHierarchyPlannerDescendantMode mode)
        {
            var svc = arg.Request.Service.GetGenericArguments()[0];
            var parameter = arg.Request.Parameters.OfType<IComponentHierarchyParameter>().First();
            var components = parameter.GetComponentsUnderPath(GetContextPath(arg, 1), mode);
            var requiredComponent = components.FirstOrDefault(x => svc.IsInstanceOfType(x));
            if (requiredComponent == null)
            {
                throw new NeedsComponentInHierarchyToContinueException(svc);
            }
            var concrete = (IInternalHasComponent)arg.Kernel.Get(typeof(DefaultRequireComponent<>).MakeGenericType(svc));
            concrete.Component = requiredComponent;
            return concrete;
        }

        private object[] GetContextPath(IContext context, int skip = 0)
        {
            var hierarchy = new List<object>();
            var parent = context.Request.ParentRequest;
            var i = 0;
            while (parent != null)
            {
                // TODO This is incorrect in the scenario where a service injects more
                // than one component of the same type.  In that scenario we need to be
                // distinguishing between parameters in the hierarchy, not just types (so
                // maybe base this off the target as well?)
                if (i++ >= skip)
                {
                    hierarchy.Add(parent.Service);
                }
                parent = parent.ParentRequest;
            }
            hierarchy.Reverse();
            return hierarchy.ToArray();
        }
    }
}
#endif