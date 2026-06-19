namespace ArturRios.Data.Interfaces;

public interface IReadOnlyRepository<out T> where T : Entity
{
    IQueryable<T> GetAll();
    T? GetById(int id);
}
