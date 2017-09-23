using System;
using System.Reflection;
using JsonRpc;
using Lsp.Capabilities.Client;
using Lsp.Models;

namespace Lsp
{
    class HandlerDescriptor : ILspHandlerDescriptor, IDisposable
    {
        private readonly Action _disposeAction;

        public HandlerDescriptor(string method, IJsonRpcHandler handler, Type handlerType, Type @params, Type registrationType, Type capabilityType, Action disposeAction)
        {
            if (method == null)
                throw new ArgumentNullException(nameof(method));

            _disposeAction = disposeAction;
            Handler = handler;
            Method = method;
            HandlerType = handlerType;
            Params = @params;
            RegistrationType = registrationType;
            CapabilityType = capabilityType;
        }

        public IJsonRpcHandler Handler { get; }
        public Type HandlerType { get; }

        public bool HasRegistration => RegistrationType != null;
        public Type RegistrationType { get; }

        public bool HasCapability => CapabilityType != null;
        public Type CapabilityType { get; }

        private Registration _registration;

        public Registration Registration
        {
            get {
                if (!HasRegistration) return null;
                if (_registration != null) return _registration;

                // TODO: Cache this
                var options = GetType()
                    .GetTypeInfo()
                    .GetMethod(nameof(GetRegistration), BindingFlags.NonPublic | BindingFlags.Static)
                    .MakeGenericMethod(RegistrationType)
                    .Invoke(this, new object[] { Handler });

                return _registration = new Registration() {
                    Id = Guid.NewGuid().ToString(),
                    Method = Method,
                    RegisterOptions = options
                };
            }
        }

        public void SetCapability(object instance)
        {
            // TODO: Cache this
            GetType()
                .GetTypeInfo()
                .GetMethod(nameof(SetCapability), BindingFlags.NonPublic | BindingFlags.Static)
                .MakeGenericMethod(CapabilityType)
                .Invoke(this, new[] { Handler, instance });

            if (instance is DynamicCapability dc)
            {
                AllowsDynamicRegistration = dc.DynamicRegistration;
            }
        }

        public string Method { get; }
        public Type Params { get; }

        public bool IsDynamicCapability => typeof(DynamicCapability).GetTypeInfo().IsAssignableFrom(CapabilityType);
        public bool AllowsDynamicRegistration { get; private set; }

        public void Dispose()
        {
            _disposeAction();
        }

        private static object GetRegistration<T>(IRegistration<T> registration)
        {
            return registration.GetRegistrationOptions();
        }

        private static void SetCapability<T>(ICapability<T> capability, T instance)
        {
            capability.SetCapability(instance);
        }

        public override bool Equals(object obj)
        {
            if (obj is HandlerDescriptor handler)
            {
                return handler.Method == Method;
            }
            return false;
        }

        public override int GetHashCode()
        {
            return Method.GetHashCode();
        }
    }
}
