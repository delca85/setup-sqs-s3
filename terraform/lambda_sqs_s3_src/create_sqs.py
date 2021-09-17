import boto3
import logging
import pprint

logger = logging.getLogger()
logger.setLevel(logging.INFO)
pp = pprint.PrettyPrinter(indent=2)

environment_map = {
    "thor": {"alias": "t"},
    "prod": {"alias": "p"},
}

sqs_client = boto3.client("sqs")
sqs_resources = boto3.resource("sqs")
queue_name_regex = (
    r"^(?P<environmentPrefix>[tp])-janus-(?P<service>[a-z]*)(-dl)?_?(?P<branch>.*)?$"
)

SQS_ARN_PREFIX = "arn:aws:sqs:eu-west-3:161959740628:"
S3_BUCKET_ARN = "arn:aws:s3:::my-bucket-for-playing"

def create_janus_queue_handler(event=None, context=None):
    """Creates janus queue in the proper environment based on json input.

    Parameters
    ----------

    event : dict
        {
            "environment": "thor",
            "service": "edward-test",
            "branch_name": "branch-name"
        }

    Returns
    -------

    dict
        Useful things that include sqs url, sqs arn, and subscription arn
    """
    environment = event["environment"]
    branch_name = event["branch_name"]
    service_name = event["service"]

    return create_janus_queue(
        environment,
        service_name,
        branch_name
    )

def get_queue_name(environment_prefix, service_name, branch_name):
    """Returns the correct queue name"""
    if branch_name == "master" or branch_name is None:
        queue_name = f"{environment_prefix}-janus-{service_name}"
    else:
        queue_name = f"{environment_prefix}-janus-{service_name}_{branch_name}"

    return queue_name

def get_policy(queue_name, environment_prefix):

    return """{
        "Version": "2012-10-17",
        "Statement": [ {
            "Effect": "Allow",
            "Principal": "*",
            "Action": "SQS:SendMessage",
            "Resource": "%s%s",
            "Condition":
                {
                "ArnEquals":
                    {
                    "aws:SourceArn":
                        "%s"
                    }
                }
            } ]
    }""" % (
        SQS_ARN_PREFIX,
        queue_name,
        S3_BUCKET_ARN
    )


def create_janus_queue(
    environment, service_name, branch_name
):
    environment_prefix = environment_map[environment]["alias"]
    queue_name = get_queue_name(environment_prefix, service_name, branch_name)
    response = {}
    tags = {
        "Environment": environment,
        "Group": service_name,
        "Branch": branch_name,
        "Role": "janus-queue",
        "Service": service_name,
        # "Tribe": get_service_tribe_mapping(service_name),
    }

    queue_policy = get_policy(queue_name, environment_prefix)

    logger.info(f"Creating queue {queue_name}, tags={tags}.")

    # Create queue
    queue = sqs_resources.create_queue(
        QueueName=queue_name, Attributes={"Policy": queue_policy}, tags=tags
    )

    sqs_url = queue.url
    sqs_arn = queue.attributes.get("QueueArn")

    response["sqs_url"] = sqs_url
    response["sqs_arn"] = sqs_arn
    response["status"] = "Created"
    return response