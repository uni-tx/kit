using System;
using System.Linq;
using NUnit.Framework;

namespace UniTx.IoC.Tests.EditMode
{
    public interface IService
    {
        string Name { get; }
    }

    public interface IOtherService
    {
    }

    public sealed class ServiceA : IService
    {
        public string Name => "A";
    }

    public sealed class ServiceB : IService, IOtherService
    {
        public string Name => "B";
    }

    public sealed class NeedsArguments : IService
    {
        public NeedsArguments(int _)
        {
        }

        public string Name => "NeedsArguments";
    }

    public class IocContainerTests
    {
        private UniContainer _container;

        [SetUp]
        public void SetUp() => _container = new UniContainer();

        [Test]
        public void Bind_Resolve_ReturnsInstance()
        {
            _container.Bind<ServiceA>().AsSingleton().Conclude();

            Assert.IsNotNull(_container.Resolve<IService>());
            Assert.AreEqual("A", _container.Resolve<IService>().Name);
        }

        [Test]
        public void AsSingleton_ReturnsSameInstance()
        {
            _container.Bind<ServiceA>().AsSingleton().Conclude();

            Assert.AreSame(_container.Resolve<IService>(), _container.Resolve<IService>());
        }

        [Test]
        public void AsTransient_ReturnsNewInstanceEachResolve()
        {
            _container.Bind<ServiceA>().AsTransient();

            Assert.AreNotSame(_container.Resolve<IService>(), _container.Resolve<IService>());
        }

        [Test]
        public void ResolveAll_ReturnsAllBoundInstances()
        {
            _container.Bind<ServiceA>().AsSingleton().Conclude();
            _container.Bind<ServiceB>().AsSingleton().Conclude();

            Assert.AreEqual(2, _container.ResolveAll<IService>().Count());
        }

        [Test]
        public void ResolveAll_UnboundContract_ReturnsEmpty()
            => CollectionAssert.IsEmpty(_container.ResolveAll<IService>());

        [Test]
        public void ResolveAll_ToleratesBindingDuringEnumeration()
        {
            _container.Bind<ServiceA>().AsSingleton().Conclude();

            // The bulk inject/initialize loading step binds while walking a ResolveAll pass;
            // without an internal snapshot that invalidates the enumerator.
            Assert.DoesNotThrow(() =>
            {
                foreach (var _ in _container.ResolveAll<IService>())
                {
                    _container.Bind<ServiceB>().AsSingleton().Conclude();
                }
            });
        }

        [Test]
        public void Unbind_RemovesConcreteAndInterfaceRegistrations()
        {
            _container.Bind<ServiceA>().AsSingleton().Conclude();
            _container.Unbind<ServiceA>();

            Assert.Throws<InvalidOperationException>(() => _container.Resolve<IService>());
            Assert.Throws<InvalidOperationException>(() => _container.Resolve<ServiceA>());
        }

        [Test]
        public void Unbind_LeavesOtherBindingsForSameContract()
        {
            _container.Bind<ServiceA>().AsSingleton().Conclude();
            _container.Bind<ServiceB>().AsSingleton().Conclude();

            _container.Unbind<ServiceA>();

            Assert.AreEqual("B", _container.Resolve<IService>().Name);
        }

        [Test]
        public void UnbindAll_ClearsEverything()
        {
            _container.Bind<ServiceA>().AsSingleton().Conclude();
            _container.Bind<ServiceB>().AsSingleton().Conclude();

            _container.UnbindAll();

            Assert.IsFalse(_container.IsBound<IService>());
        }

        [Test]
        public void Resolve_UnboundType_Throws()
            => Assert.Throws<InvalidOperationException>(() => _container.Resolve<IService>());

        [Test]
        public void TryResolve_Unbound_ReturnsFalseWithoutThrowing()
        {
            Assert.IsFalse(_container.TryResolve<IService>(out var service));
            Assert.IsNull(service);
        }

        [Test]
        public void TryResolve_Bound_ReturnsTrueWithInstance()
        {
            _container.Bind<ServiceA>().AsSingleton().Conclude();

            Assert.IsTrue(_container.TryResolve<IService>(out var service));
            Assert.AreEqual("A", service.Name);
        }

        [Test]
        public void IsBound_ReflectsRegistration()
        {
            Assert.IsFalse(_container.IsBound<IService>());

            _container.Bind<ServiceA>().AsSingleton().Conclude();

            Assert.IsTrue(_container.IsBound<IService>());
        }

        [Test]
        public void BindInstance_RegistersProvidedInstance()
        {
            var instance = new ServiceB();

            _container.BindInstance(instance).AsSingleton().Conclude();

            Assert.AreSame(instance, _container.Resolve<IService>());
            Assert.AreSame(instance, _container.Resolve<IOtherService>());
        }

        [Test]
        public void BindInstance_AcceptsTypeWithoutParameterlessConstructor()
        {
            // Bind<T> requires new(); BindInstance exists precisely for types the container
            // cannot construct itself.
            var instance = new NeedsArguments(1);

            Assert.DoesNotThrow(() => _container.BindInstance(instance).AsSingleton().Conclude());
            Assert.AreSame(instance, _container.Resolve<IService>());
        }

        [Test]
        public void BindInstance_Null_Throws()
            => Assert.Throws<ArgumentNullException>(() => _container.BindInstance<ServiceA>(null));

        [Test]
        public void Bind_Interface_Throws()
            => Assert.Throws<ArgumentException>(() => _container.Bind(typeof(IService)));

        [Test]
        public void Bind_MismatchedInstance_Throws()
            => Assert.Throws<ArgumentException>(() => _container.Bind(typeof(ServiceA), new ServiceB()));

        [Test]
        public void AsTransient_AfterBindInstance_Throws()
        {
            // A transient binding constructs per resolve, which cannot honour a supplied
            // instance — failing loudly beats silently ignoring one of the two.
            var binding = _container.BindInstance(new ServiceA());

            Assert.Throws<InvalidOperationException>(() => binding.AsTransient());
        }

        [Test]
        public void Resolve_TypeWithoutParameterlessConstructor_ThrowsExplainingBindInstance()
        {
            _container.Bind(typeof(NeedsArguments));

            var ex = Assert.Throws<InvalidOperationException>(() => _container.Resolve<IService>());
            StringAssert.Contains("BindInstance", ex.Message);
        }
    }
}
