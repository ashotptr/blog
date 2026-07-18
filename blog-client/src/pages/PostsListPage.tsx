import { useEffect, useState } from 'react';
import api from '../api/axios';
import PostCard from '../components/PostCard';
import type { PostSummary } from '../types';

const PostsListPage = () => {
  const [posts, setPosts] = useState<PostSummary[]>([]);
  const [query, setQuery] = useState('');
  const [activeQuery, setActiveQuery] = useState('');
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const fetchPosts = async (searchTerm: string) => {
    setLoading(true);
    setError(null);
    try {
      const response = searchTerm
        ? await api.get<PostSummary[]>('/api/blogposts/search', { params: { query: searchTerm } })
        : await api.get<PostSummary[]>('/api/blogposts');
      setPosts(response.data);
    } catch (err) {
      console.error('Error loading posts:', err);
      setError('Could not load posts. Is the API running?');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchPosts(activeQuery);
  }, [activeQuery]);

  const handleSearch = (e: React.FormEvent) => {
    e.preventDefault();
    setActiveQuery(query.trim());
  };

  return (
    <div>
      <form onSubmit={handleSearch} className="search-bar">
        <input
          type="search"
          placeholder="Search posts…"
          value={query}
          onChange={e => setQuery(e.target.value)}
        />
        <button type="submit">Search</button>
        {activeQuery && (
          <button
            type="button"
            className="link-button"
            onClick={() => {
              setQuery('');
              setActiveQuery('');
            }}
          >
            Clear
          </button>
        )}
      </form>

      {loading && <p className="status-line">Loading posts…</p>}
      {error && <p className="form-error">{error}</p>}

      {!loading && !error && posts.length === 0 && (
        <p className="status-line">
          {activeQuery ? `No posts match “${activeQuery}”.` : 'No posts yet.'}
        </p>
      )}

      {posts.map(post => (
        <PostCard key={post.id} post={post} />
      ))}
    </div>
  );
};

export default PostsListPage;
