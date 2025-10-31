import { Popover, ListGroup } from 'react-bootstrap';
import classes from './eqClasses';

export default function PlayerData(data: IRaidAttendance) {
	return (
		<Popover>
			<Popover.Header>{data.name}</Popover.Header>
			<Popover.Body>
				<ListGroup horizontal>
					<ListGroup.Item className='text-info'>Rank</ListGroup.Item>
					<ListGroup.Item className='flex-fill'>{data.rank}</ListGroup.Item>
				</ListGroup>
				<ListGroup horizontal>
					<ListGroup.Item className='text-info'>Class</ListGroup.Item>
					<ListGroup.Item className='flex-fill'>{classes[data.class]}</ListGroup.Item>
				</ListGroup>
				<ListGroup horizontal>
					<ListGroup.Item className='text-info'>Level</ListGroup.Item>
					<ListGroup.Item className='flex-fill'>{data.level || '-'}</ListGroup.Item>
				</ListGroup>
				<ListGroup horizontal>
					<ListGroup.Item className='text-info'>Last On Date</ListGroup.Item>
					<ListGroup.Item className='flex-fill'>{data.lastOnDate || '-'}</ListGroup.Item>
				</ListGroup>
				<ListGroup horizontal>
					<ListGroup.Item className='text-info'>Zone</ListGroup.Item>
					<ListGroup.Item className='flex-fill'>{data.zone || '-'}</ListGroup.Item>
				</ListGroup>
				<ListGroup horizontal>
					<ListGroup.Item className='text-info'>Granted T1 ToB Raid Loots</ListGroup.Item>
					<ListGroup.Item className='flex-fill'>{data.t1GrantedLootCount}</ListGroup.Item>
				</ListGroup>
				<ListGroup horizontal>
					<ListGroup.Item className='text-info'>Granted T2 ToB Raid Loots</ListGroup.Item>
					<ListGroup.Item className='flex-fill'>{data.t2GrantedLootCount}</ListGroup.Item>
				</ListGroup>
				<hr />
				<h6 className='text-info'>Notes</h6>
				<p>{data.notes}</p>
				<hr />
				<h6 className='text-info'>Alts</h6>
				<ListGroup>
					{data.alts.map(x =>
						<ListGroup.Item key={x}>{x}</ListGroup.Item>
					)}
				</ListGroup>
			</Popover.Body>
		</Popover>
	);
}