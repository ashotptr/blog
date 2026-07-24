import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import api from '../api/axios';
import { useAuth } from '../contexts/AuthContext';
import type { PostSummary } from '../types';

const DashboardPage = () => {
  const [posts, setPosts] = useState<PostSummary[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const { user, hasRole } = useAuth();

  const isAdmin = hasRole('Admin');

  const fetchPosts = async () => {
    setLoading(true);
    setError(null);
    try {
      const response = await api.get<PostSummary[]>('/api/blogposts');
      setPosts(response.data);
    } catch {
      setError('Could not load posts.');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchPosts();
  }, []);

  const handleDelete = async (id: number) => {
    if (!window.confirm('Delete this post permanently?')) return;
    try {
      await api.delete(`/api/blogposts/${id}`);
      setPosts(prev => prev.filter(post => post.id !== id));
    } catch {
      setError('Could not delete the post.');
    }
  };

  const visiblePosts = isAdmin ? posts : posts.filter(post => post.authorName === user?.name);
  const canWrite = hasRole('Admin', 'Writer');

  return (
    <div>
      <div className="dashboard-header">
        <h1>{isAdmin ? 'All posts' : 'My posts'}</h1>
        {canWrite && <Link to="/posts/new" className="button-link">New post</Link>}
      </div>

      <p className="status-line">Signed in as <strong>{user?.name}</strong> ({user?.roles.join(', ') || 'no role'})</p>

      {loading && <p className="status-line">Loading…</p>}
      {error && <p className="form-error">{error}</p>}

      {!loading && visiblePosts.length === 0 && (
        <p className="status-line">
          {canWrite ? 'No posts yet — write your first one!' : 'Nothing to manage: your account has read access.'}
        </p>
      )}

      <ul className="dashboard-list">
        {visiblePosts.map(post => (
          <li key={post.id}>
            <div>
              <Link to={`/posts/${post.id}`}>{post.title}</Link>
              <span className="post-meta"> · {new Date(post.publishedDate).toLocaleDateString()}{isAdmin ? ` · ${post.authorName}` : ''}</span>
            </div>
            {(canWrite || isAdmin) && (
              <div className="dashboard-actions">
                <Link to={`/posts/${post.id}/edit`}>Edit</Link>
                <button type="button" className="link-button danger" onClick={() => handleDelete(post.id)}>
                  Delete
                </button>
              </div>
            )}
          </li>
        ))}
      </ul>
    </div>
  );
};

export default DashboardPage;
