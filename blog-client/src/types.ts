export interface PostSummary {
  id: number;
  title: string;
  excerpt: string;
  publishedDate: string;
  authorName: string;
  tags: string[];
}

export interface PostDetail {
  id: number;
  title: string;
  content: string;
  publishedDate: string;
  authorName: string;
  authorId: string | null;
  tags: string[];
}
