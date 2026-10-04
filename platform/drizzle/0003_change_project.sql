ALTER TABLE "change" ADD COLUMN "project_path" text DEFAULT '' NOT NULL;--> statement-breakpoint
ALTER TABLE "change" ADD COLUMN "project_id" integer;--> statement-breakpoint
ALTER TABLE "change" ADD CONSTRAINT "change_project_id_project_id_fk" FOREIGN KEY ("project_id") REFERENCES "public"."project"("id") ON DELETE set null ON UPDATE no action;--> statement-breakpoint
CREATE INDEX "change_project_occurred_at" ON "change" USING btree ("project_id","occurred_at");