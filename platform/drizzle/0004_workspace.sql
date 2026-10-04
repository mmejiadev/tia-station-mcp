CREATE TABLE "folder" (
	"id" integer PRIMARY KEY GENERATED ALWAYS AS IDENTITY (sequence name "folder_id_seq" INCREMENT BY 1 MINVALUE 1 MAXVALUE 2147483647 START WITH 1 CACHE 1),
	"organization_id" text NOT NULL,
	"parent_id" integer,
	"name" text NOT NULL,
	"created_at" timestamp with time zone DEFAULT now() NOT NULL
);
--> statement-breakpoint
ALTER TABLE "project" ADD COLUMN "folder_id" integer;--> statement-breakpoint
ALTER TABLE "station" ADD COLUMN "organization_id" text;--> statement-breakpoint
ALTER TABLE "folder" ADD CONSTRAINT "folder_organization_id_organization_id_fk" FOREIGN KEY ("organization_id") REFERENCES "public"."organization"("id") ON DELETE cascade ON UPDATE no action;--> statement-breakpoint
ALTER TABLE "folder" ADD CONSTRAINT "folder_parent_id_folder_id_fk" FOREIGN KEY ("parent_id") REFERENCES "public"."folder"("id") ON DELETE cascade ON UPDATE no action;--> statement-breakpoint
CREATE INDEX "folder_organization_parent" ON "folder" USING btree ("organization_id","parent_id");--> statement-breakpoint
ALTER TABLE "project" ADD CONSTRAINT "project_folder_id_folder_id_fk" FOREIGN KEY ("folder_id") REFERENCES "public"."folder"("id") ON DELETE set null ON UPDATE no action;--> statement-breakpoint
ALTER TABLE "station" ADD CONSTRAINT "station_organization_id_organization_id_fk" FOREIGN KEY ("organization_id") REFERENCES "public"."organization"("id") ON DELETE set null ON UPDATE no action;