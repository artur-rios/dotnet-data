namespace ArturRios.Data.Interfaces;

public interface ICrudRepository<T> where T : Entity
{
    int Create(T entity);
    IQueryable<T> GetAll();
    T? GetById(int id);
    T Update(T entity);
    int Delete(T entity);
}
