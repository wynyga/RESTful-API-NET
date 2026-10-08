"use client";

import { useQuery } from "@tanstack/react-query";

import { useProfile } from "@/components/AppFrame";
import { Badge, Card, EmptyState, ErrorState, PageHeader, Select, SkeletonRows } from "@/components/ui";
import { api } from "@/lib/api";
import { useAction } from "@/lib/hooks";

export default function AdminPage() {
  const profile = useProfile();
  const isAdmin = profile.role === "Admin";

  const users = useQuery({ queryKey: ["admin-users"], queryFn: api.admin.users, enabled: isAdmin });
  const setRole = useAction(api.admin.setRole, { success: "Role updated", invalidate: ["admin-users"] });

  if (!isAdmin) {
    return (
      <>
        <PageHeader title="Users" />
        <Card>
          <EmptyState title="Admins only" hint="Your account can read projects but cannot manage users." />
        </Card>
      </>
    );
  }

  return (
    <>
      <PageHeader title="Users" subtitle="Everyone with an account, and what they may do." />

      <Card className="overflow-hidden">
        {users.isPending ? (
          <SkeletonRows />
        ) : users.isError ? (
          <ErrorState message={users.error.message} onRetry={() => users.refetch()} />
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full min-w-[28rem] text-left text-sm">
              <thead className="border-b border-line bg-surface2 text-xs text-muted">
                <tr>
                  <th scope="col" className="px-5 py-3 font-medium">Email</th>
                  <th scope="col" className="px-5 py-3 font-medium">Role</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-line">
                {users.data.map((u) => {
                  const self = u.email.toLowerCase() === profile.email.toLowerCase();
                  return (
                    <tr key={u.id} className="hover:bg-surface2">
                      <td className="px-5 py-3.5">
                        {u.email} {self && <Badge>You</Badge>}
                      </td>
                      <td className="px-5 py-3.5">
                        {self ? (
                          <span className="text-muted">{u.role} (your own role cannot be changed here)</span>
                        ) : (
                          <Select
                            label={`Role for ${u.email}`}
                            value={u.role}
                            onChange={(role) => setRole.run(u.id, role as "Admin" | "User")}
                            options={[
                              { value: "User", label: "User" },
                              { value: "Admin", label: "Admin" },
                            ]}
                            className="w-32"
                          />
                        )}
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        )}
      </Card>
      <p className="mt-3 text-xs text-muted">A role change takes effect the next time that user signs in, because the role is stored in their token.</p>
    </>
  );
}
