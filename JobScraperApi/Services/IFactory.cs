namespace EazeTechnical.Services;

public interface IFactory<T>
{
    T Create(params object[] args);
}