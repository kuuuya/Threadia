export interface AuthResponse {
  userId: string;
  email: string;
  displayName: string;
  token: string;
  expiresAt: string;
}

export interface User {
  id: string;
  email: string;
  displayName: string;
}

export interface Workspace {
  id: string;
  name: string;
  createdBy: string;
  createdAt: string;
}

export interface WorkspaceMember {
  userId: string;
  displayName: string;
  email: string;
  role: string;
  joinedAt: string;
}

export type ConversationType = "Direct" | "Group";

export interface Conversation {
  id: string;
  workspaceId: string;
  type: ConversationType;
  name: string | null;
  createdBy: string;
  createdAt: string;
  memberIds: string[];
}

export interface MessageAttachment {
  id: string;
  fileName: string;
  contentType: string;
  size: number;
}

export interface Message {
  id: string;
  conversationId: string;
  sequence: number;
  senderId: string;
  clientMessageId: string;
  content: string;
  createdAt: string;
  editedAt: string | null;
  isDeleted: boolean;
  mentionedUserIds: string[];
  attachments: MessageAttachment[];
}

export interface MessagePage {
  items: Message[];
  hasMore: boolean;
}

export interface UnreadCount {
  conversationId: string;
  latestSequence: number;
  lastReadSequence: number;
  unreadCount: number;
}

export interface ReadPosition {
  conversationId: string;
  lastReadSequence: number;
  updatedAt: string;
}

export interface UserPresence {
  userId: string;
  isOnline: boolean;
}

export interface UploadTicket {
  attachmentId: string;
  uploadUrl: string;
  expiresAt: string;
}

export interface DownloadUrl {
  url: string;
  expiresAt: string;
}

export interface SearchResult {
  messageId: string;
  conversationId: string;
  sequence: number;
  senderId: string;
  snippet: string;
  createdAt: string;
}

export interface SearchPage {
  items: SearchResult[];
  hasMore: boolean;
}

export interface AppNotification {
  id: string;
  type: string;
  conversationId: string;
  messageId: string;
  title: string;
  body: string;
  createdAt: string;
  readAt: string | null;
}
